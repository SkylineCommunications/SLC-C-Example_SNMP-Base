using System;
using System.Collections.Generic;
using System.Linq;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;

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
			var interfaceTablesRowData = new Dictionary<string, InterfaceTablesRowData>();
			var duplexStatusValues = GetDuplexStatus(protocol);

			// ifTable
			var interfaceTable = new IfTable(protocol);
			for (var i = 0; i < interfaceTable.Keys.Length; i++)
			{
				var interfaceStatesTableRow = new InterfacesQActionRow();
				var interfacesCountersRxTableRow = new InterfacesdetailsrxQActionRow();
				var interfacesCountersTxTableRow = new InterfacesdetailstxQActionRow();

				PopulateDataFromInterfaceTable(interfaceStatesTableRow, interfacesCountersRxTableRow, interfacesCountersTxTableRow, interfaceTable, i);

				var key = Convert.ToString(interfaceTable.Keys[i]);
				if (duplexStatusValues.TryGetValue(key, out var duplexState))
				{
					interfaceStatesTableRow.Interfacesduplexstatus = duplexState;
				}
				else
				{
					interfaceStatesTableRow.Interfacesduplexstatus = -1; // N/A
				}

				interfaceTablesRowData.Add(key, new InterfaceTablesRowData(interfaceStatesTableRow, interfacesCountersRxTableRow, interfacesCountersTxTableRow));
			}

			// ifXTable.
			var interfaceExtendedTable = new IfXTable(protocol);
			for (var i = 0; i < interfaceExtendedTable.Keys.Length; i++)
			{
				var key = Convert.ToString(interfaceExtendedTable.Keys[i]);

				if (!interfaceTablesRowData.TryGetValue(key, out var tablesRowData))
				{
					continue;
				}

				var interfaceStatesTableRow = tablesRowData.InterfacesRow;
				var interfacesCountersRxTableRow = tablesRowData.InterfacesRxRow;
				var interfacesCountersTxTableRow = tablesRowData.InterfacesTxRow;

				PopulateDataFromInterfaceExtendedTable(interfaceStatesTableRow, interfacesCountersRxTableRow, interfacesCountersTxTableRow, interfaceExtendedTable, i);

				interfacesCountersRxTableRow.Interfacesdetailsrxrate = GetSummablePacketRates(interfacesCountersRxTableRow).Sum();
				interfacesCountersTxTableRow.Interfacesdetailstxrate = GetSummablePacketRates(interfacesCountersTxTableRow).Sum();
			}

			var rowCount = interfaceTablesRowData.Count;
			var rows = interfaceTablesRowData.Values.ToArray();
			var interfaceRows = new QActionTableRow[rowCount];
			var interfaceDetailsRxRows = new QActionTableRow[rowCount];
			var interfaceDetailsTxRows = new QActionTableRow[rowCount];

			for (var i = 0; i < rowCount; i++)
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

	private static IEnumerable<double> GetSummablePacketRates(InterfacesdetailsrxQActionRow row)
	{
		yield return Convert.ToDouble(row.Interfacesdetailsrxunicastrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailsrxbroadcastrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailsrxmulticastrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailsrxdiscardrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailsrxerrorrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailsrxunknownprotocolrate).ZeroIfException(-1);
	}

	private static IEnumerable<double> GetSummablePacketRates(InterfacesdetailstxQActionRow row)
	{
		yield return Convert.ToDouble(row.Interfacesdetailstxunicastrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailstxbroadcastrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailstxmulticastrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailstxdiscardrate).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacesdetailstxerrorrate).ZeroIfException(-1);
	}

	private static Dictionary<string, int> GetDuplexStatus(SLProtocol protocol)
	{
		var duplexStatusesPerKey = new Dictionary<string, int>();

		var columnsToGet = new uint[] { Parameter.Dot3stats.Idx.dot3stats_index, Parameter.Dot3stats.Idx.dot3stats_duplexstatus };

		var columns = protocol.GetColumns(Parameter.Dot3stats.tablePid, columnsToGet);
		var keys = (object[])columns[0];
		var duplexStatuses = (object[])columns[1];

		for (var i = 0; i < keys.Length; i++)
		{
			duplexStatusesPerKey[Convert.ToString(keys[i])] = Convert.ToInt32(duplexStatuses[i]);
		}

		return duplexStatusesPerKey;
	}

	private static void PopulateDataFromInterfaceTable(
		InterfacesQActionRow interfaceTableRow,
		InterfacesdetailsrxQActionRow interfacesRxTableRow,
		InterfacesdetailstxQActionRow interfacesTxTableRow,
		IfTable interfaceTable,
		int getPosition)
	{
		// Keys
		var key = Convert.ToString(interfaceTable.Keys[getPosition]);
		interfaceTableRow.Interfacesindex = key;
		interfacesRxTableRow.Interfacesdetailsrxdindex = interfacesRxTableRow.Interfacesdetailsrxfktointerfaces = key;
		interfacesTxTableRow.Interfacesdetailstxindex = interfacesTxTableRow.Interfacesdetailstxfktointerfaces = key;

		// Interface Table
		interfaceTableRow.Interfacestype = Convert.ToDouble(interfaceTable.Types[getPosition]);
		interfaceTableRow.Interfacesmtu = Convert.ToDouble(interfaceTable.Mtu[getPosition]);
		interfaceTableRow.Interfacesdescription = Convert.ToString(interfaceTable.Descriptions[getPosition]);
		interfaceTableRow.Interfacesadminstatus = Convert.ToDouble(interfaceTable.AdminStatus[getPosition]);
		interfaceTableRow.Interfacesoperstatus = Convert.ToDouble(interfaceTable.OperStatus[getPosition]);
		interfaceTableRow.Interfaceslastchange = Convert.ToDouble(interfaceTable.LastChange[getPosition]);

		if (Convert.ToUInt32(interfaceTable.Speeds[getPosition]) != MaxReportableIfSpeed)
		{
			// Speed in ifTable is expressed in bps, whereas speed in Interface Table is expressed in Mbps.
			interfaceTableRow.Interfacesspeed = Convert.ToDouble(interfaceTable.Speeds[getPosition]) / Math.Pow(10, 6);
		}

		// Discards, Errors, Unknown Protocols only have 32-bit counters.
		interfacesRxTableRow.Interfacesdetailsrxdiscardrate = Convert.ToDouble(interfaceTable.DiscardRateIn[getPosition]);
		interfacesRxTableRow.Interfacesdetailsrxerrorrate = Convert.ToDouble(interfaceTable.ErrorRateIn[getPosition]);
		interfacesRxTableRow.Interfacesdetailsrxunknownprotocolrate = Convert.ToDouble(interfaceTable.UnknownProtocolRateIn[getPosition]);

		interfacesTxTableRow.Interfacesdetailstxdiscardrate = Convert.ToDouble(interfaceTable.DiscardRateOut[getPosition]);
		interfacesTxTableRow.Interfacesdetailstxerrorrate = Convert.ToDouble(interfaceTable.ErrorRateOut[getPosition]);

		var useHighCapacityCounters = ShouldUseHighCapacityCounters(Convert.ToDouble(interfaceTable.Speeds[getPosition]));
		if (useHighCapacityCounters)
		{
			return;
		}

		interfaceTableRow.Interfacesrxoctets = Convert.ToDouble(interfaceTable.InOctets[getPosition]);
		interfaceTableRow.Interfacestxoctets = Convert.ToDouble(interfaceTable.OutOctets[getPosition]);

		var dBitRateIn = Convert.ToDouble(interfaceTable.BitRateIn[getPosition]);
		interfaceTableRow.Interfacesrxbitrate = dBitRateIn >= 0 ? dBitRateIn / Math.Pow(10, 6) : -1; // bps -> Mbps

		var dBitRateOut = Convert.ToDouble(interfaceTable.BitRateOut[getPosition]);
		interfaceTableRow.Interfacestxbitrate = dBitRateOut >= 0 ? dBitRateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

		interfaceTableRow.Interfacesbandwidthutilization = Convert.ToDouble(interfaceTable.BandwidthUtilization[getPosition]);

		interfacesRxTableRow.Interfacesdetailsrxunicastrate = Convert.ToDouble(interfaceTable.UnicastRateIn[getPosition]);
		interfacesTxTableRow.Interfacesdetailstxunicastrate = Convert.ToDouble(interfaceTable.UnicastRateOut[getPosition]);
	}

	private static bool ShouldUseHighCapacityCounters(double interfaceSpeed)
	{
		return interfaceSpeed > SpeedLimitForCounters;
	}

	private static void PopulateDataFromInterfaceExtendedTable(
		InterfacesQActionRow interfaceTableRow,
		InterfacesdetailsrxQActionRow interfacesRxTableRow,
		InterfacesdetailstxQActionRow interfacesTxTableRow,
		IfXTable interfaceExtendedTable,
		int getPosition)
	{
		interfaceTableRow.Interfaceslogical =
			interfaceExtendedTable.ConnectorPresent[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.ConnectorPresent[getPosition]);
		interfaceTableRow.Interfaceslastclear =
			interfaceExtendedTable.CounterDiscontinuityTime[getPosition] == null ? 0 : Convert.ToDouble(interfaceExtendedTable.CounterDiscontinuityTime[getPosition]) / 100;
		interfaceTableRow.Interfacesuserdescription = Convert.ToString(interfaceExtendedTable.Alias[getPosition]);
		interfaceTableRow.Interfacespromiscuousmode =
			interfaceExtendedTable.PromiscuousMode[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.PromiscuousMode[getPosition]);
		interfaceTableRow.Interfaceslinkupdowntrap = Convert.ToDouble(interfaceExtendedTable.LinkUpDownTrapEnable[getPosition]);

		if (interfaceExtendedTable.HighSpeed[getPosition] != null)
		{
			interfaceTableRow.Interfacesspeed = Convert.ToDouble(interfaceExtendedTable.HighSpeed[getPosition]);
		}

		var isUsingHighCapacityCounters = interfaceTableRow.Interfacesrxoctets == null;
		if (isUsingHighCapacityCounters)
		{
			PopulateHighCapacityDataFromInterfaceExtendedTable(interfaceTableRow, interfacesRxTableRow, interfacesTxTableRow, interfaceExtendedTable, getPosition);
		}
		else
		{
			PopulateLowCapacityDataFromInterfaceExtendedTable(interfaceTableRow, interfacesRxTableRow, interfacesTxTableRow, interfaceExtendedTable, getPosition);
		}
	}

	private static void PopulateLowCapacityDataFromInterfaceExtendedTable(
		InterfacesQActionRow interfaceTableRow,
		InterfacesdetailsrxQActionRow interfacesRxTableRow,
		InterfacesdetailstxQActionRow interfacesTxTableRow,
		IfXTable interfaceExtendedTable,
		int getPosition)
	{
		// Interface Counters RX Table
		interfacesRxTableRow.Interfacesdetailsrxbroadcastrate =
			interfaceExtendedTable.BroadcastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.BroadcastRateIn[getPosition]);
		interfacesRxTableRow.Interfacesdetailsrxmulticastrate =
			interfaceExtendedTable.MulticastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.MulticastRateIn[getPosition]);

		// Interface Counters TX Table
		interfacesTxTableRow.Interfacesdetailstxbroadcastrate =
			interfaceExtendedTable.BroadcastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.BroadcastRateOut[getPosition]);
		interfacesTxTableRow.Interfacesdetailstxmulticastrate =
			interfaceExtendedTable.MulticastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.MulticastRateOut[getPosition]);

		var bitrateIn = Convert.ToDouble(interfaceExtendedTable.HcBitRateIn[getPosition]);
		if (bitrateIn < -1)
		{
			// Indication of discontinuity times, need to set values to N/A
			interfaceTableRow.Interfacesrxbitrate = -1;
			interfaceTableRow.Interfacestxbitrate = -1;
			interfaceTableRow.Interfacesbandwidthutilization = -1;
		}
	}

	private static void PopulateHighCapacityDataFromInterfaceExtendedTable(
		InterfacesQActionRow interfaceTableRow,
		InterfacesdetailsrxQActionRow interfacesRxTableRow,
		InterfacesdetailstxQActionRow interfacesTxTableRow,
		IfXTable interfaceExtendedTable,
		int getPosition)
	{
		// Interface States Table
		interfaceTableRow.Interfacesrxoctets = interfaceExtendedTable.HcInOctets[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcInOctets[getPosition]);
		interfaceTableRow.Interfacestxoctets =
			interfaceExtendedTable.HcOutOctets[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcOutOctets[getPosition]);
		interfaceTableRow.Interfacesbandwidthutilization = Convert.ToDouble(interfaceExtendedTable.BandwidthUtilization[getPosition]);

		var bitrateIn = Convert.ToDouble(interfaceExtendedTable.HcBitRateIn[getPosition]);
		interfaceTableRow.Interfacesrxbitrate = bitrateIn >= 0 ? bitrateIn / Math.Pow(10, 6) : -1; // bps -> Mbps

		var bitrateOut = Convert.ToDouble(interfaceExtendedTable.HcBitRateOut[getPosition]);
		interfaceTableRow.Interfacestxbitrate = bitrateOut >= 0 ? bitrateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

		// Interface Counters RX Table
		interfacesRxTableRow.Interfacesdetailsrxunicastrate =
			interfaceExtendedTable.HcUnicastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcUnicastRateIn[getPosition]);
		interfacesRxTableRow.Interfacesdetailsrxbroadcastrate =
			interfaceExtendedTable.HcBroadcastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcBroadcastRateIn[getPosition]);
		interfacesRxTableRow.Interfacesdetailsrxmulticastrate =
			interfaceExtendedTable.HcMulticastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcMulticastRateIn[getPosition]);

		// Interface Counters TX Table
		interfacesTxTableRow.Interfacesdetailstxunicastrate =
			interfaceExtendedTable.HcUnicastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcUnicastRateOut[getPosition]);
		interfacesTxTableRow.Interfacesdetailstxbroadcastrate =
			interfaceExtendedTable.HcBroadcastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcBroadcastRateOut[getPosition]);
		interfacesTxTableRow.Interfacesdetailstxmulticastrate =
			interfaceExtendedTable.HcMulticastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcMulticastRateOut[getPosition]);
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