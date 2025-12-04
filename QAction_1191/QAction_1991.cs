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
				var interfaceCountersRxTableRow = new InterfacedetailsrxQActionRow();
				var interfaceCountersTxTableRow = new InterfacedetailstxQActionRow();

				PopulateDataFromInterfaceTable(interfaceStatesTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, interfaceTable, i);

				var key = Convert.ToString(interfaceTable.Keys[i]);
				if (duplexStatusValues.TryGetValue(key, out var duplexState))
				{
					interfaceStatesTableRow.Interfacesduplexstatus_2018 = duplexState;
				}
				else
				{
					interfaceStatesTableRow.Interfacesduplexstatus_2018 = -1; // N/A
				}

				interfaceTablesRowData.Add(key, new InterfaceTablesRowData(interfaceStatesTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow));
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
				var interfaceCountersRxTableRow = tablesRowData.InterfaceDetailsRxRow;
				var interfaceCountersTxTableRow = tablesRowData.InterfaceDetailsTxRow;

				PopulateDataFromInterfaceExtendedTable(interfaceStatesTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, interfaceExtendedTable, i);

				interfaceCountersRxTableRow.Interfacedetailsrxrate_2102 = GetSummablePacketRates(interfaceCountersRxTableRow).Sum();
				interfaceCountersTxTableRow.Interfacedetailstxrate_2202 = GetSummablePacketRates(interfaceCountersTxTableRow).Sum();
			}

			var rowCount = interfaceTablesRowData.Count;
			var rows = interfaceTablesRowData.Values.ToArray();
			var interfaceRows = new QActionTableRow[rowCount];
			var interfaceDetailsRxRows = new QActionTableRow[rowCount];
			var interfaceDetailsTxRows = new QActionTableRow[rowCount];

			for (var i = 0; i < rowCount; i++)
			{
				interfaceRows[i] = rows[i].InterfacesRow;
				interfaceDetailsRxRows[i] = rows[i].InterfaceDetailsRxRow;
				interfaceDetailsTxRows[i] = rows[i].InterfaceDetailsTxRow;
			}

			protocol.interfaces.FillArray(interfaceRows);
			protocol.interfacedetailsrx.FillArray(interfaceDetailsRxRows);
			protocol.interfacedetailstx.FillArray(interfaceDetailsTxRows);
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}

	private static IEnumerable<double> GetSummablePacketRates(InterfacedetailsrxQActionRow row)
	{
		yield return Convert.ToDouble(row.Interfacedetailsrxunicastrate_2103).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailsrxbroadcastrate_2104).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailsrxmulticastrate_2105).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailsrxdiscardrate_2106).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailsrxerrorrate_2107).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailsrxunknownprotocolrate_2108).ZeroIfException(-1);
	}

	private static IEnumerable<double> GetSummablePacketRates(InterfacedetailstxQActionRow row)
	{
		yield return Convert.ToDouble(row.Interfacedetailstxunicastrate_2203).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailstxbroadcastrate_2204).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailstxmulticastrate_2205).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailstxdiscardrate_2206).ZeroIfException(-1);
		yield return Convert.ToDouble(row.Interfacedetailstxerrorrate_2207).ZeroIfException(-1);
	}

	private static Dictionary<string, int> GetDuplexStatus(SLProtocol protocol)
	{
		var duplexStatusesPerKey = new Dictionary<string, int>();

		var columnsToGet = new uint[] { Parameter.Dot3stats.Idx.dot3stats_index_1301, Parameter.Dot3stats.Idx.dot3stats_duplexstatus_1302 };

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
		InterfacedetailsrxQActionRow interfaceCountersRxTableRow,
		InterfacedetailstxQActionRow interfaceCountersTxTableRow,
		IfTable interfaceTable,
		int getPosition)
	{
		// Keys
		var key = Convert.ToString(interfaceTable.Keys[getPosition]);
		interfaceTableRow.Interfacesindex_2001 = key;
		interfaceCountersRxTableRow.Interfacedetailsrxdex_2101 = interfaceCountersRxTableRow.Interfacedetailsrxfktointerfaces_2109 = key;
		interfaceCountersTxTableRow.Interfacedetailstxindex_2201 = interfaceCountersTxTableRow.Interfacedetailstxfktointerfaces_2208 = key;

		// Interface Table
		interfaceTableRow.Interfacestype_2002 = Convert.ToDouble(interfaceTable.Types[getPosition]);
		interfaceTableRow.Interfacesmtu_2003 = Convert.ToDouble(interfaceTable.Mtu[getPosition]);
		interfaceTableRow.Interfacesdescription_2004 = Convert.ToString(interfaceTable.Descriptions[getPosition]);
		interfaceTableRow.Interfacesadminstatus_2005 = Convert.ToDouble(interfaceTable.AdminStatus[getPosition]);
		interfaceTableRow.Interfacesoperstatus_2006 = Convert.ToDouble(interfaceTable.OperStatus[getPosition]);
		interfaceTableRow.Interfaceslastchange_2007 = Convert.ToDouble(interfaceTable.LastChange[getPosition]);

		if (Convert.ToUInt32(interfaceTable.Speeds[getPosition]) != MaxReportableIfSpeed)
		{
			// Speed in ifTable is expressed in bps, whereas speed in Interface Table is expressed in Mbps.
			interfaceTableRow.Interfacesspeed_2016 = Convert.ToDouble(interfaceTable.Speeds[getPosition]) / Math.Pow(10, 6);
		}

		// Discards, Errors, Unknown Protocols only have 32-bit counters.
		interfaceCountersRxTableRow.Interfacedetailsrxdiscardrate_2106 = Convert.ToDouble(interfaceTable.DiscardRateIn[getPosition]);
		interfaceCountersRxTableRow.Interfacedetailsrxerrorrate_2107 = Convert.ToDouble(interfaceTable.ErrorRateIn[getPosition]);
		interfaceCountersRxTableRow.Interfacedetailsrxunknownprotocolrate_2108 = Convert.ToDouble(interfaceTable.UnknownProtocolRateIn[getPosition]);

		interfaceCountersTxTableRow.Interfacedetailstxdiscardrate_2206 = Convert.ToDouble(interfaceTable.DiscardRateOut[getPosition]);
		interfaceCountersTxTableRow.Interfacedetailstxerrorrate_2207 = Convert.ToDouble(interfaceTable.ErrorRateOut[getPosition]);

		var useHighCapacityCounters = ShouldUseHighCapacityCounters(Convert.ToDouble(interfaceTable.Speeds[getPosition]));
		if (useHighCapacityCounters)
		{
			return;
		}

		interfaceTableRow.Interfacesrxoctets_2009 = Convert.ToDouble(interfaceTable.InOctets[getPosition]);
		interfaceTableRow.Interfacestxoctets_2010 = Convert.ToDouble(interfaceTable.OutOctets[getPosition]);

		var dBitRateIn = Convert.ToDouble(interfaceTable.BitRateIn[getPosition]);
		interfaceTableRow.Interfacesrxbitrate_2013 = dBitRateIn >= 0 ? dBitRateIn / Math.Pow(10, 6) : -1; // bps -> Mbps

		var dBitRateOut = Convert.ToDouble(interfaceTable.BitRateOut[getPosition]);
		interfaceTableRow.Interfacestxbitrate_2014 = dBitRateOut >= 0 ? dBitRateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

		interfaceTableRow.Interfacesbandwidthutilization_2017 = Convert.ToDouble(interfaceTable.BandwidthUtilization[getPosition]);

		interfaceCountersRxTableRow.Interfacedetailsrxunicastrate_2103 = Convert.ToDouble(interfaceTable.UnicastRateIn[getPosition]);
		interfaceCountersTxTableRow.Interfacedetailstxunicastrate_2203 = Convert.ToDouble(interfaceTable.UnicastRateOut[getPosition]);
	}

	private static bool ShouldUseHighCapacityCounters(double interfaceSpeed)
	{
		return interfaceSpeed > SpeedLimitForCounters;
	}

	private static void PopulateDataFromInterfaceExtendedTable(
		InterfacesQActionRow interfaceTableRow,
		InterfacedetailsrxQActionRow interfaceCountersRxTableRow,
		InterfacedetailstxQActionRow interfaceCountersTxTableRow,
		IfXTable interfaceExtendedTable,
		int getPosition)
	{
		interfaceTableRow.Interfaceslogical_2008 =
			interfaceExtendedTable.ConnectorPresent[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.ConnectorPresent[getPosition]);
		interfaceTableRow.Interfaceslastclear_2011 =
			interfaceExtendedTable.CounterDiscontinuityTime[getPosition] == null ? 0 : Convert.ToDouble(interfaceExtendedTable.CounterDiscontinuityTime[getPosition]) / 100;
		interfaceTableRow.Interfacesuserdescription_2015 = Convert.ToString(interfaceExtendedTable.Alias[getPosition]);
		interfaceTableRow.Interfacespromiscuousmode_2019 =
			interfaceExtendedTable.PromiscuousMode[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.PromiscuousMode[getPosition]);
		interfaceTableRow.Interfaceslinkupdowntrap_2020 = Convert.ToDouble(interfaceExtendedTable.LinkUpDownTrapEnable[getPosition]);

		if (interfaceExtendedTable.HighSpeed[getPosition] != null)
		{
			interfaceTableRow.Interfacesspeed_2016 = Convert.ToDouble(interfaceExtendedTable.HighSpeed[getPosition]);
		}

		var isUsingHighCapacityCounters = interfaceTableRow.Interfacesrxoctets_2009 == null;
		if (isUsingHighCapacityCounters)
		{
			PopulateHighCapacityDataFromInterfaceExtendedTable(interfaceTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, interfaceExtendedTable, getPosition);
		}
		else
		{
			PopulateLowCapacityDataFromInterfaceExtendedTable(interfaceTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, interfaceExtendedTable, getPosition);
		}
	}

	private static void PopulateLowCapacityDataFromInterfaceExtendedTable(
		InterfacesQActionRow interfaceTableRow,
		InterfacedetailsrxQActionRow interfaceCountersRxTableRow,
		InterfacedetailstxQActionRow interfaceCountersTxTableRow,
		IfXTable interfaceExtendedTable,
		int getPosition)
	{
		// Interface Counters RX Table
		interfaceCountersRxTableRow.Interfacedetailsrxbroadcastrate_2104 =
			interfaceExtendedTable.BroadcastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.BroadcastRateIn[getPosition]);
		interfaceCountersRxTableRow.Interfacedetailsrxmulticastrate_2105 =
			interfaceExtendedTable.MulticastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.MulticastRateIn[getPosition]);

		// Interface Counters TX Table
		interfaceCountersTxTableRow.Interfacedetailstxbroadcastrate_2204 =
			interfaceExtendedTable.BroadcastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.BroadcastRateOut[getPosition]);
		interfaceCountersTxTableRow.Interfacedetailstxmulticastrate_2205 =
			interfaceExtendedTable.MulticastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.MulticastRateOut[getPosition]);

		var bitrateIn = Convert.ToDouble(interfaceExtendedTable.HcBitRateIn[getPosition]);
		if (bitrateIn < -1)
		{
			// Indication of discontinuity times, need to set values to N/A
			interfaceTableRow.Interfacesrxbitrate_2013 = -1;
			interfaceTableRow.Interfacestxbitrate_2014 = -1;
			interfaceTableRow.Interfacesbandwidthutilization_2017 = -1;
		}
	}

	private static void PopulateHighCapacityDataFromInterfaceExtendedTable(
		InterfacesQActionRow interfaceTableRow,
		InterfacedetailsrxQActionRow interfaceCountersRxTableRow,
		InterfacedetailstxQActionRow interfaceCountersTxTableRow,
		IfXTable interfaceExtendedTable,
		int getPosition)
	{
		// Interface States Table
		interfaceTableRow.Interfacesrxoctets_2009 = interfaceExtendedTable.HcInOctets[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcInOctets[getPosition]);
		interfaceTableRow.Interfacestxoctets_2010 =
			interfaceExtendedTable.HcOutOctets[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcOutOctets[getPosition]);
		interfaceTableRow.Interfacesbandwidthutilization_2017 = Convert.ToDouble(interfaceExtendedTable.BandwidthUtilization[getPosition]);

		var bitrateIn = Convert.ToDouble(interfaceExtendedTable.HcBitRateIn[getPosition]);
		interfaceTableRow.Interfacesrxbitrate_2013 = bitrateIn >= 0 ? bitrateIn / Math.Pow(10, 6) : -1; // bps -> Mbps

		var bitrateOut = Convert.ToDouble(interfaceExtendedTable.HcBitRateOut[getPosition]);
		interfaceTableRow.Interfacestxbitrate_2014 = bitrateOut >= 0 ? bitrateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

		// Interface Counters RX Table
		interfaceCountersRxTableRow.Interfacedetailsrxunicastrate_2103 =
			interfaceExtendedTable.HcUnicastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcUnicastRateIn[getPosition]);
		interfaceCountersRxTableRow.Interfacedetailsrxbroadcastrate_2104 =
			interfaceExtendedTable.HcBroadcastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcBroadcastRateIn[getPosition]);
		interfaceCountersRxTableRow.Interfacedetailsrxmulticastrate_2105 =
			interfaceExtendedTable.HcMulticastRateIn[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcMulticastRateIn[getPosition]);

		// Interface Counters TX Table
		interfaceCountersTxTableRow.Interfacedetailstxunicastrate_2203 =
			interfaceExtendedTable.HcUnicastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcUnicastRateOut[getPosition]);
		interfaceCountersTxTableRow.Interfacedetailstxbroadcastrate_2204 =
			interfaceExtendedTable.HcBroadcastRateOut[getPosition] == null ? -1 : Convert.ToDouble(interfaceExtendedTable.HcBroadcastRateOut[getPosition]);
		interfaceCountersTxTableRow.Interfacedetailstxmulticastrate_2205 =
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