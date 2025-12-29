using System;
using System.Collections.Generic;
using System.Linq;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;
using Skyline.Protocol.Interfaces;

/// <summary>
/// DataMiner QAction Class: Merge Interface Tables.
/// </summary>
public static class QAction
{
	private const uint MaxReportableIfSpeed = uint.MaxValue;

	/* RFC 2863:
	 * - For interfaces that operate at 20,000,000 (20 million) bits per second or less, 32-bit byte and packet counters MUST be supported.
	 *
	 * - For interfaces that operate faster than 20,000,000 bits/second, and slower than 650,000,000 bits/second, 32-bit packet counters MUST be
	 * supported and 64-bit octet counters MUST be supported.
	 *
	 * - For interfaces that operate at 650,000,000 bits/second or faster, 64-bit packet counters AND 64-bit octet counters MUST be supported.
	 * We choose to use 64-bit counters if the speed is higher than 20Mbps as this will result in fewer wraparounds.
	 */
	private const double SpeedLimitForCounters = 20000000;

	/// <summary>
	/// The QAction entry point.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		try
		{
			var interfacesRowsPerKey = new Dictionary<string, InterfaceTablesRowData>();
			var duplexStatusesPerKey = DuplexGetter.GetDuplexStatusesByKey(protocol);

			// ifTable.
			var ifTableGetter = new IfTableGetter(protocol);
			for (int i = 0; i < ifTableGetter.Keys.Length; i++)
			{
				var interfacesRow = new InterfacesQActionRow();
				var interfacesDetailsRxRow = new InterfacesdetailsrxQActionRow();
				var interfacesDetailsTxRow = new InterfacesdetailstxQActionRow();

				PopulateDataFromIfTable(interfacesRow, interfacesDetailsRxRow, interfacesDetailsTxRow, ifTableGetter, i);

				string key = Convert.ToString(ifTableGetter.Keys[i]);
				if (duplexStatusesPerKey.TryGetValue(key, out int duplexState))
				{
					interfacesRow.Interfacesduplexstatus = duplexState;
				}
				else
				{
					interfacesRow.Interfacesduplexstatus = -1; // N/A
				}

				interfacesRowsPerKey.Add(key, new InterfaceTablesRowData(interfacesRow, interfacesDetailsRxRow, interfacesDetailsTxRow));
			}

			// ifXTable.
			var ifXTableGetter = new IfXTableGetter(protocol);
			for (int i = 0; i < ifXTableGetter.Keys.Length; i++)
			{
				string key = Convert.ToString(ifXTableGetter.Keys[i]);

				if (!interfacesRowsPerKey.TryGetValue(key, out var tablesRowData))
				{
					continue;
				}

				var interfacesTableRow = tablesRowData.InterfacesRow;
				var interfacesDetailsRxRow = tablesRowData.InterfacesRxRow;
				var interfacesDetailsTxRow = tablesRowData.InterfacesTxRow;

				PopulateDataFromIfXTable(interfacesTableRow, interfacesDetailsRxRow, interfacesDetailsTxRow, ifXTableGetter, i);
			}

			// Interfaces tables.
			var rows = interfacesRowsPerKey.Values.ToArray();
			var interfaceRows = new QActionTableRow[rows.Length];
			var interfaceDetailsRxRows = new QActionTableRow[rows.Length];
			var interfaceDetailsTxRows = new QActionTableRow[rows.Length];

			for (int i = 0; i < rows.Length; i++)
			{
				interfaceRows[i] = rows[i].InterfacesRow;
				interfaceDetailsRxRows[i] = rows[i].InterfacesRxRow;
				interfaceDetailsTxRows[i] = rows[i].InterfacesTxRow;
			}

			protocol.interfaces.FillArray(interfaceRows);
			protocol.interfacesdetailsrx.FillArray(interfaceDetailsRxRows);
			protocol.interfacesdetailstx.FillArray(interfaceDetailsTxRows);
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}

	private static void PopulateDataFromIfTable(
		InterfacesQActionRow interfacesRow,
		InterfacesdetailsrxQActionRow interfacesRxRow,
		InterfacesdetailstxQActionRow interfacesTxRow,
		IfTableGetter ifTableGetter,
		int getPosition)
	{
		// Keys
		string key = Convert.ToString(ifTableGetter.Keys[getPosition]);
		interfacesRow.Interfacesindex = key;
		interfacesRxRow.Interfacesdetailsrxdindex = interfacesRxRow.Interfacesdetailsrxfktointerfaces = key;
		interfacesTxRow.Interfacesdetailstxindex = interfacesTxRow.Interfacesdetailstxfktointerfaces = key;

		// Interface Table
		interfacesRow.Interfacestype = Convert.ToDouble(ifTableGetter.Types[getPosition]);
		interfacesRow.Interfacesmtu = Convert.ToDouble(ifTableGetter.Mtu[getPosition]);
		interfacesRow.Interfacesdescription = Convert.ToString(ifTableGetter.Descriptions[getPosition]);
		interfacesRow.Interfacesadminstatus = Convert.ToDouble(ifTableGetter.AdminStatus[getPosition]);
		interfacesRow.Interfacesoperstatus = Convert.ToDouble(ifTableGetter.OperStatus[getPosition]);
		interfacesRow.Interfaceslastchange = Convert.ToDouble(ifTableGetter.LastChange[getPosition]);

		if (Convert.ToUInt32(ifTableGetter.Speeds[getPosition]) != MaxReportableIfSpeed)
		{
			// Speed in ifTable is expressed in bps, whereas speed in Interface Table is expressed in Mbps.
			interfacesRow.Interfacesspeed = Convert.ToDouble(ifTableGetter.Speeds[getPosition]) / Math.Pow(10, 6);
		}

		// Discards, Errors, Unknown Protocols only have 32-bit counters.
		interfacesRxRow.Interfacesdetailsrxdiscardrate = Convert.ToDouble(ifTableGetter.DiscardRateIn[getPosition]);
		interfacesRxRow.Interfacesdetailsrxerrorrate = Convert.ToDouble(ifTableGetter.ErrorRateIn[getPosition]);
		interfacesRxRow.Interfacesdetailsrxunknownprotocolrate = Convert.ToDouble(ifTableGetter.UnknownProtocolRateIn[getPosition]);

		interfacesTxRow.Interfacesdetailstxdiscardrate = Convert.ToDouble(ifTableGetter.DiscardRateOut[getPosition]);
		interfacesTxRow.Interfacesdetailstxerrorrate = Convert.ToDouble(ifTableGetter.ErrorRateOut[getPosition]);

		bool useHighCapacityCounters = ShouldUseHighCapacityCounters(Convert.ToDouble(ifTableGetter.Speeds[getPosition]));
		if (useHighCapacityCounters)
		{
			return;
		}

		interfacesRow.Interfacesrxoctets = Convert.ToDouble(ifTableGetter.InOctets[getPosition]);
		interfacesRow.Interfacestxoctets = Convert.ToDouble(ifTableGetter.OutOctets[getPosition]);

		double dBitRateIn = Convert.ToDouble(ifTableGetter.BitRateIn[getPosition]);
		interfacesRow.Interfacesrxbitrate = dBitRateIn >= 0 ? dBitRateIn / Math.Pow(10, 6) : -1; // bps -> Mbps

		double dBitRateOut = Convert.ToDouble(ifTableGetter.BitRateOut[getPosition]);
		interfacesRow.Interfacestxbitrate = dBitRateOut >= 0 ? dBitRateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

		interfacesRow.Interfacesbandwidthutilization = Convert.ToDouble(ifTableGetter.BandwidthUtilization[getPosition]);

		interfacesRxRow.Interfacesdetailsrxunicastrate = Convert.ToDouble(ifTableGetter.UnicastRateIn[getPosition]);
		interfacesTxRow.Interfacesdetailstxunicastrate = Convert.ToDouble(ifTableGetter.UnicastRateOut[getPosition]);
	}

	private static bool ShouldUseHighCapacityCounters(double interfaceSpeed)
	{
		return interfaceSpeed > SpeedLimitForCounters;
	}

	private static void PopulateDataFromIfXTable(
		InterfacesQActionRow interfacesRow,
		InterfacesdetailsrxQActionRow interfacesRxRow,
		InterfacesdetailstxQActionRow interfacesTxRow,
		IfXTableGetter ifXTableGetter,
		int getPosition)
	{
		interfacesRow.Interfaceslogical = ifXTableGetter.ConnectorPresent[getPosition] == null
			? -1
			: Convert.ToDouble(ifXTableGetter.ConnectorPresent[getPosition]);

		interfacesRow.Interfaceslastclear = ifXTableGetter.CounterDiscontinuityTime[getPosition] == null
			? 0
			: Convert.ToDouble(ifXTableGetter.CounterDiscontinuityTime[getPosition]) / 100;

		interfacesRow.Interfacesuserdescription = Convert.ToString(ifXTableGetter.Alias[getPosition]);

		interfacesRow.Interfacespromiscuousmode = ifXTableGetter.PromiscuousMode[getPosition] == null
			? -1
			: Convert.ToDouble(ifXTableGetter.PromiscuousMode[getPosition]);

		interfacesRow.Interfaceslinkupdowntrap = Convert.ToDouble(ifXTableGetter.LinkUpDownTrapEnable[getPosition]);

		if (ifXTableGetter.HighSpeed[getPosition] != null)
		{
			interfacesRow.Interfacesspeed = Convert.ToDouble(ifXTableGetter.HighSpeed[getPosition]);
		}

		bool isUsingHighCapacityCounters = interfacesRow.Interfacesrxoctets == null;
		if (isUsingHighCapacityCounters)
		{
			PopulateHighCapacityDataFromIfXTable(interfacesRow, interfacesRxRow, interfacesTxRow, ifXTableGetter, getPosition);
		}
		else
		{
			PopulateLowCapacityDataFromIfXTable(interfacesRow, interfacesRxRow, interfacesTxRow, ifXTableGetter, getPosition);
		}
	}

	private static void PopulateLowCapacityDataFromIfXTable(
		InterfacesQActionRow interfacesRow,
		InterfacesdetailsrxQActionRow interfacesRxRow,
		InterfacesdetailstxQActionRow interfacesTxRow,
		IfXTableGetter ifXTableGetter,
		int getPosition)
	{
		// Interface Counters RX Table
		interfacesRxRow.Interfacesdetailsrxbroadcastrate = ifXTableGetter.BroadcastRateIn[getPosition] == null
			? -1
			: Convert.ToDouble(ifXTableGetter.BroadcastRateIn[getPosition]);

		interfacesRxRow.Interfacesdetailsrxmulticastrate = ifXTableGetter.MulticastRateIn[getPosition] == null
			? -1
			: Convert.ToDouble(ifXTableGetter.MulticastRateIn[getPosition]);

		// Interface Counters TX Table
		interfacesTxRow.Interfacesdetailstxbroadcastrate = ifXTableGetter.BroadcastRateOut[getPosition] == null
			? -1
			: Convert.ToDouble(ifXTableGetter.BroadcastRateOut[getPosition]);

		interfacesTxRow.Interfacesdetailstxmulticastrate = ifXTableGetter.MulticastRateOut[getPosition] == null
			? -1
			: Convert.ToDouble(ifXTableGetter.MulticastRateOut[getPosition]);

		double bitrateIn = Convert.ToDouble(ifXTableGetter.HcBitRateIn[getPosition]);
		if (bitrateIn < -1)
		{
			// Indication of discontinuity times, need to set values to N/A
			interfacesRow.Interfacesrxbitrate = -1;
			interfacesRow.Interfacestxbitrate = -1;
			interfacesRow.Interfacesbandwidthutilization = -1;
		}
	}

	private static void PopulateHighCapacityDataFromIfXTable(
		InterfacesQActionRow interfacesRow,
		InterfacesdetailsrxQActionRow interfacesRxRow,
		InterfacesdetailstxQActionRow interfacesTxRow,
		IfXTableGetter ifXTableGetter,
		int getPosition)
	{
		// Interface States Table
		interfacesRow.Interfacesrxoctets =
			ifXTableGetter.HcInOctets[getPosition] == null ? -1 : Convert.ToDouble(ifXTableGetter.HcInOctets[getPosition]);

		interfacesRow.Interfacestxoctets =
			ifXTableGetter.HcOutOctets[getPosition] == null ? -1 : Convert.ToDouble(ifXTableGetter.HcOutOctets[getPosition]);

		interfacesRow.Interfacesbandwidthutilization = Convert.ToDouble(ifXTableGetter.BandwidthUtilization[getPosition]);

		double bitrateIn = Convert.ToDouble(ifXTableGetter.HcBitRateIn[getPosition]);
		interfacesRow.Interfacesrxbitrate = bitrateIn >= 0 ? bitrateIn / Math.Pow(10, 6) : -1; // bps -> Mbps

		double bitrateOut = Convert.ToDouble(ifXTableGetter.HcBitRateOut[getPosition]);
		interfacesRow.Interfacestxbitrate = bitrateOut >= 0 ? bitrateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

		// Interface Counters RX Table
		interfacesRxRow.Interfacesdetailsrxunicastrate =
			ifXTableGetter.HcUnicastRateIn[getPosition] == null ? -1 : Convert.ToDouble(ifXTableGetter.HcUnicastRateIn[getPosition]);

		interfacesRxRow.Interfacesdetailsrxbroadcastrate =
			ifXTableGetter.HcBroadcastRateIn[getPosition] == null ? -1 : Convert.ToDouble(ifXTableGetter.HcBroadcastRateIn[getPosition]);

		interfacesRxRow.Interfacesdetailsrxmulticastrate =
			ifXTableGetter.HcMulticastRateIn[getPosition] == null ? -1 : Convert.ToDouble(ifXTableGetter.HcMulticastRateIn[getPosition]);

		// Interface Counters TX Table
		interfacesTxRow.Interfacesdetailstxunicastrate =
			ifXTableGetter.HcUnicastRateOut[getPosition] == null ? -1 : Convert.ToDouble(ifXTableGetter.HcUnicastRateOut[getPosition]);

		interfacesTxRow.Interfacesdetailstxbroadcastrate =
			ifXTableGetter.HcBroadcastRateOut[getPosition] == null ? -1 : Convert.ToDouble(ifXTableGetter.HcBroadcastRateOut[getPosition]);

		interfacesTxRow.Interfacesdetailstxmulticastrate =
			ifXTableGetter.HcMulticastRateOut[getPosition] == null ? -1 : Convert.ToDouble(ifXTableGetter.HcMulticastRateOut[getPosition]);
	}
}

internal static class RateDoubleExtensions
{
	public static double ZeroIfException(this double value, double exception, double tolerance = 1e-6)
	{
		var isException = Math.Abs(value - exception) < tolerance;
		return isException ? 0 : value;
	}
}