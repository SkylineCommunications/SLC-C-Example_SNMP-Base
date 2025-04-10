using System;
using System.Collections.Generic;
using System.Linq;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;

using SLNetMessages = Skyline.DataMiner.Net.Messages;

/// <summary>
/// DataMiner QAction Class: Merge Interface Tables.
/// </summary>
public class QAction
{
	private const uint MaxReportableIfSpeed = UInt32.MaxValue;

	// RFC 2863: For interfaces that operate at 20,000,000 (20 million) bits per 	second or less, 32-bit byte and packet counters MUST be supported.
	// For interfaces that operate faster than 20,000,000 bits/second, and 	slower than 650,000,000 bits/second, 32-bit packet counters MUST be
	// supported and 64-bit octet counters MUST be supported.For interfaces that operate at 650,000,000 bits/second or faster, 64-bit packet counters AND 64-bit octet counters MUST be supported.
	// We choose to use 64-bit counters if the speed is higher than 20Mbps as this will result in fewer wraparounds.
	private const double SpeedLimitForCounters = 20000000;

	/// <summary>
	/// The QAction entry point.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		try
		{
			Dictionary<string, InterfacesStateQActionRows> interfacesStateRows = new Dictionary<string, InterfacesStateQActionRows>();
			Dictionary<string, int> duplexStatusValues = GetDuplexStatus(protocol);

			// ifTable
			IfTable iftable = new IfTable(protocol);
			for (int i = 0; i < iftable.Keys.Length; i++)
			{
				InterfacesstateQActionRow interfaceStatesTableRow = new InterfacesstateQActionRow();
				InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow = new InterfacesstatecountersrxQActionRow();
				InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow = new InterfacesstatecounterstxQActionRow();

				MergeFromSnmpIfTable(interfaceStatesTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, iftable, i);

				string key = Convert.ToString(iftable.Keys[i]);
				if (duplexStatusValues.TryGetValue(key, out int duplexState))
				{
					interfaceStatesTableRow.Interfacesstateduplexstatus_2018 = duplexState;
				}
				else
				{
					interfaceStatesTableRow.Interfacesstateduplexstatus_2018 = -1; // N/A
				}

				interfacesStateRows.Add(key, new InterfacesStateQActionRows(interfaceStatesTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow));
			}

			// ifXTable.
			IfXTable ifxtable = new IfXTable(protocol);
			for (int i = 0; i < ifxtable.Keys.Length; i++)
			{
				string key = Convert.ToString(ifxtable.Keys[i]);

				if (interfacesStateRows.TryGetValue(key, out InterfacesStateQActionRows interfacesStateQActionRows))
				{
					InterfacesstateQActionRow interfaceStatesTableRow = interfacesStateQActionRows.InterfacesStateTableRow;
					InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow = interfacesStateQActionRows.InterfacesCountersRxTableRow;
					InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow = interfacesStateQActionRows.InterfacesCountersTxTableRows;

					MergeFromSnmpIfXTable(interfaceStatesTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, ifxtable, i);

					// Interface Counters RX Table
					interfaceCountersRxTableRow.Interfacesstatecountersrxinrate_2102 = new[]
					{
						Convert.ToDouble(interfaceCountersRxTableRow.Interfacesstatecountersrxinunicastrate_2103),
						Convert.ToDouble(interfaceCountersRxTableRow.Interfacesstatecountersrxinbroadcastrate_2104),
						Convert.ToDouble(interfaceCountersRxTableRow.Interfacesstatecountersrxinmulticastrate_2105),
						Convert.ToDouble(interfaceCountersRxTableRow.Interfacesstatecountersrxindiscardsrate_2106),
						Convert.ToDouble(interfaceCountersRxTableRow.Interfacesstatecountersrxinerrorsrate_2107),
						Convert.ToDouble(interfaceCountersRxTableRow.Interfacesstatecountersrxinunknownprotosrate_2108),
					}.Sum();

					// Interface Counters TX Table
					interfaceCountersTxTableRow.Interfacesstatecounterstxoutrate_2202 = new[]
					{
						Convert.ToDouble(interfaceCountersTxTableRow.Interfacesstatecounterstxoutunicastrate_2203),
						Convert.ToDouble(interfaceCountersTxTableRow.Interfacesstatecounterstxoutbroadcastrate_2204),
						Convert.ToDouble(interfaceCountersTxTableRow.Interfacesstatecounterstxoutmulticastrate_2205),
						Convert.ToDouble(interfaceCountersTxTableRow.Interfacesstatecounterstxoutdiscardsrate_2206),
						Convert.ToDouble(interfaceCountersTxTableRow.Interfacesstatecounterstxouterrorsrate_2207),
					}.Sum();
				}
			}

			int count = interfacesStateRows.Count;
			InterfacesStateQActionRows[] x = interfacesStateRows.Values.ToArray();
			InterfacesstateQActionRow[] interfacesStateArr = new InterfacesstateQActionRow[count];
			InterfacesstatecountersrxQActionRow[] interfacesStateCountersRxArr = new InterfacesstatecountersrxQActionRow[count];
			InterfacesstatecounterstxQActionRow[] interfacesStateCountersTxArr = new InterfacesstatecounterstxQActionRow[count];

			for (int i = 0; i < count; i++)
			{
				interfacesStateArr[i] = x[i].InterfacesStateTableRow;
				interfacesStateCountersRxArr[i] = x[i].InterfacesCountersRxTableRow;
				interfacesStateCountersTxArr[i] = x[i].InterfacesCountersTxTableRows;
			}

			protocol.Log(String.Join(Environment.NewLine, interfacesStateArr.Select(i => String.Join(";", ((object[])i).Select(j => Convert.ToString(j))))));

			// protocol.FillArray(Parameter.Interfacesstate.tablePid, interfacesStateArr.Select(row => row.ToObjectArray()).ToList(), NotifyProtocol.SaveOption.Full);
			protocol.interfacesstate.FillArray(interfacesStateArr);
			protocol.interfacesstatecountersrx.FillArray(interfacesStateCountersRxArr);
			protocol.interfacesstatecounterstx.FillArray(interfacesStateCountersTxArr);
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|Run|Error: {ex}", LogType.Error, LogLevel.NoLogging);
		}
	}

	private static Dictionary<string, int> GetDuplexStatus(SLProtocol protocol)
	{
		Dictionary<string, int> duplexStatusesPerKey = new Dictionary<string, int>();

		uint[] columnsToGet = new uint[]
		{
			Parameter.Dot3statstable.Idx.dot3statsindex_1301,
			Parameter.Dot3statstable.Idx.dot3statsduplexstatus_1302,
		};

		object[] columns = protocol.GetColumns(Parameter.Dot3statstable.tablePid, columnsToGet);
		object[] keys = (object[])columns[0];
		object[] duplexStatuses = (object[])columns[1];

		for (int i = 0; i < keys.Length; i++)
		{
			duplexStatusesPerKey[Convert.ToString(keys[i])] = Convert.ToInt32(duplexStatuses[i]);
		}

		return duplexStatusesPerKey;
	}

	private static void MergeFromSnmpIfTable(InterfacesstateQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfTable iftable, int getPosition)
	{
		// Keys
		string key = Convert.ToString(iftable.Keys[getPosition]);
		interfaceTableRow.Interfacesstateindex_2001 = key;
		interfaceCountersRxTableRow.Interfacesstatecountersrxindex_2101 = interfaceCountersRxTableRow.Interfacesstatecountersrxfktointerfaces_2109 = key;
		interfaceCountersTxTableRow.Interfacesstatecounterstxindex_2201 = interfaceCountersTxTableRow.Interfacesstatecounterstxfktointerfaces_2208 = key;

		// Interface States Table
		interfaceTableRow.Interfacesstatetype_2002 = Convert.ToDouble(iftable.Types[getPosition]);
		interfaceTableRow.Interfacesstatemtu_2003 = Convert.ToDouble(iftable.MTUs[getPosition]);
		interfaceTableRow.Interfacesstatedescription_2004 = Convert.ToString(iftable.Descriptions[getPosition]);
		interfaceTableRow.Interfacesstateadminstatus_2005 = Convert.ToDouble(iftable.AdminStatus[getPosition]);
		interfaceTableRow.Interfacesstateoperstatus_2006 = Convert.ToDouble(iftable.OperStatus[getPosition]);
		interfaceTableRow.Interfacesstatelastchange_2007 = Convert.ToDouble(iftable.LastChange[getPosition]);

		if (Convert.ToUInt32(iftable.Speeds[getPosition]) != MaxReportableIfSpeed)
		{
			// Speed in ifTable is expressed in bps, whereas speed in Interface table is expressed in Mbps.
			interfaceTableRow.Interfacesstatespeed_2016 = Convert.ToDouble(iftable.Speeds[getPosition]) / Math.Pow(10, 6);
		}

		if (Convert.ToDouble(iftable.Speeds[getPosition]) <= SpeedLimitForCounters)
		{
			// This means we should use the 32-bit versions.
			// Interface States Table
			interfaceTableRow.Interfacesstateinoctets_2009 = Convert.ToDouble(iftable.InOctets[getPosition]);
			interfaceTableRow.Interfacesstateoutoctets_2010 = Convert.ToDouble(iftable.OutOctets[getPosition]);

			//// Interface States Table - Calculated Values
			double dBitRateIn = Convert.ToDouble(iftable.BitRateIn[getPosition]);
			interfaceTableRow.Interfacesstateinbitrate_2013 = dBitRateIn >= 0 ? dBitRateIn / Math.Pow(10, 6) : -1;    // bps -> Mbps

			double dBitRateOut = Convert.ToDouble(iftable.BitRateOut[getPosition]);
			interfaceTableRow.Interfacesstateoutbitrate_2014 = dBitRateOut >= 0 ? dBitRateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

			interfaceTableRow.Interfacesstatebandwidthutilization_2017 = Convert.ToDouble(iftable.BandwidthUtilization[getPosition]);

			// Interface Counters RX Table
			interfaceCountersRxTableRow.Interfacesstatecountersrxinunicastrate_2103 = Convert.ToDouble(iftable.InUcastpkts[getPosition]);
			interfaceCountersRxTableRow.Interfacesstatecountersrxindiscardsrate_2106 = Convert.ToDouble(iftable.InDiscards[getPosition]);
			interfaceCountersRxTableRow.Interfacesstatecountersrxinerrorsrate_2107 = Convert.ToDouble(iftable.InErrors[getPosition]);
			interfaceCountersRxTableRow.Interfacesstatecountersrxinunknownprotosrate_2108 = Convert.ToDouble(iftable.InUnknownProtos[getPosition]);

			// Interface Counters TX Table
			interfaceCountersTxTableRow.Interfacesstatecounterstxoutunicastrate_2203 = Convert.ToDouble(iftable.OutUcastpkts[getPosition]);
			interfaceCountersTxTableRow.Interfacesstatecounterstxoutdiscardsrate_2206 = Convert.ToDouble(iftable.OutDiscards[getPosition]);
			interfaceCountersTxTableRow.Interfacesstatecounterstxouterrorsrate_2207 = Convert.ToDouble(iftable.OutErrors[getPosition]);
		}
	}

	private static void MergeFromSnmpIfXTable(InterfacesstateQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfXTable ifxtable, int getPosition)
	{
		interfaceTableRow.Interfacesstatelogical_2008 = ifxtable.ConnectorPresent[getPosition] == null? -1 : Convert.ToDouble(ifxtable.ConnectorPresent[getPosition]);
		interfaceTableRow.Interfacesstatelastclear_2011 = Convert.ToDouble(ifxtable.CounterDiscontinuitytime[getPosition]) / 100;
		interfaceTableRow.Interfacesstateuserdescription_2015 = Convert.ToString(ifxtable.Alias[getPosition]);
		interfaceTableRow.Interfacesstatepromiscuousmode_2019 = ifxtable.PromiscuousMode[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.PromiscuousMode[getPosition]);
		interfaceTableRow.Interfacesstatelinkupdowntrap_2020 = Convert.ToDouble(ifxtable.LinkUpDownTrapEnable[getPosition]);

		if (ifxtable.HighSpeed[getPosition] != null)
		{
			interfaceTableRow.Interfacesstatespeed_2016 = Convert.ToDouble(ifxtable.HighSpeed[getPosition]);
		}

		if (interfaceTableRow.Interfacesstateinoctets_2009 == null)
		{
			Use64BitCounters(interfaceTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, ifxtable, getPosition);
		}
		else
		{
			Use32BitCounters(interfaceTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, ifxtable, getPosition);
		}
	}

	private static void Use32BitCounters(InterfacesstateQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfXTable ifxtable, int getPosition)
	{
		// Interface Counters RX Table
		interfaceCountersRxTableRow.Interfacesstatecountersrxinbroadcastrate_2104 = ifxtable.InBroadcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.InBroadcastPkts[getPosition]);
		interfaceCountersRxTableRow.Interfacesstatecountersrxinmulticastrate_2105 = ifxtable.InMulticastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.InMulticastPkts[getPosition]);

		// Interface Counters TX Table
		interfaceCountersTxTableRow.Interfacesstatecounterstxoutbroadcastrate_2204 = ifxtable.OutBroadcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.OutBroadcastPkts[getPosition]);
		interfaceCountersTxTableRow.Interfacesstatecounterstxoutmulticastrate_2205 = ifxtable.OutMulticastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.OutMulticastPkts[getPosition]);

		double dBitrateIn = Convert.ToDouble(ifxtable.BitRateIn[getPosition]);
		if (dBitrateIn < -1)
		{
			// Indication of discontinuity times, need to set values to N/A
			interfaceTableRow.Interfacesstateinbitrate_2013 = -1;
			interfaceTableRow.Interfacesstateoutbitrate_2014 = -1;
			interfaceTableRow.Interfacesstatebandwidthutilization_2017 = -1;
		}
	}

	private static void Use64BitCounters(InterfacesstateQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfXTable ifxtable, int getPosition)
	{
		// Interface States Table
		interfaceTableRow.Interfacesstateinoctets_2009 = ifxtable.HcInOctets[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInOctets[getPosition]);
		interfaceTableRow.Interfacesstateoutoctets_2010 = ifxtable.HcOutOctets[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutOctets[getPosition]);
		interfaceTableRow.Interfacesstatebandwidthutilization_2017 = Convert.ToDouble(ifxtable.BandwidthUtilization[getPosition]);

		double bitrateIn = Convert.ToDouble(ifxtable.BitRateIn[getPosition]);
		interfaceTableRow.Interfacesstateinbitrate_2013 = bitrateIn >= 0 ? bitrateIn / Math.Pow(10, 6) : -1;        // bps -> Mbps

		double bitrateOut = Convert.ToDouble(ifxtable.BitRateOut[getPosition]);
		interfaceTableRow.Interfacesstateoutbitrate_2014 = bitrateOut >= 0 ? bitrateOut / Math.Pow(10, 6) : -1;   // bps -> Mbps

		// Interface Counters RX Table
		interfaceCountersRxTableRow.Interfacesstatecountersrxinunicastrate_2103 = ifxtable.HcInUcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInUcastPkts[getPosition]);
		interfaceCountersRxTableRow.Interfacesstatecountersrxinbroadcastrate_2104 = ifxtable.HcInBroadcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInBroadcastPkts[getPosition]);
		interfaceCountersRxTableRow.Interfacesstatecountersrxinmulticastrate_2105 = ifxtable.HcInMulticastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInMulticastPkts[getPosition]);

		// Interface Counters TX Table
		interfaceCountersTxTableRow.Interfacesstatecounterstxoutunicastrate_2203 = ifxtable.HcOutUcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutUcastPkts[getPosition]);
		interfaceCountersTxTableRow.Interfacesstatecounterstxoutbroadcastrate_2204 = ifxtable.HcOutBroadcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutBroadcastPkts[getPosition]);
		interfaceCountersTxTableRow.Interfacesstatecounterstxoutmulticastrate_2205 = ifxtable.HcOutMulticastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutMulticastPkts[getPosition]);
	}
}

public class InterfacesStateQActionRows
{
	public InterfacesStateQActionRows(InterfacesstateQActionRow interfacesStateTableRow, InterfacesstatecountersrxQActionRow interfacesCountersRxTableRow, InterfacesstatecounterstxQActionRow interfacesCountersTxTableRows)
	{
		InterfacesStateTableRow = interfacesStateTableRow;
		InterfacesCountersRxTableRow = interfacesCountersRxTableRow;
		InterfacesCountersTxTableRows = interfacesCountersTxTableRows;
	}

	public InterfacesstateQActionRow InterfacesStateTableRow { get; set; }

	public InterfacesstatecountersrxQActionRow InterfacesCountersRxTableRow { get; set; }

	public InterfacesstatecounterstxQActionRow InterfacesCountersTxTableRows { get; set; }
}

public class IfTable
{
	public IfTable(SLProtocol protocol)
	{
		uint[] columnsToGet = new uint[]
		{
			Parameter.Iftable.Idx.iftableifindex,
			Parameter.Iftable.Idx.iftableifdescr,
			Parameter.Iftable.Idx.iftableiftype,
			Parameter.Iftable.Idx.iftableifmtu,
			Parameter.Iftable.Idx.iftableifspeed,
			Parameter.Iftable.Idx.iftableifphysaddress,
			Parameter.Iftable.Idx.iftableifadminstatus,
			Parameter.Iftable.Idx.iftableifoperstatus,
			Parameter.Iftable.Idx.iftableiflastchange,
			Parameter.Iftable.Idx.iftableifinoctets,
			Parameter.Iftable.Idx.iftableifinucastpkts,
			Parameter.Iftable.Idx.iftableifindiscards,
			Parameter.Iftable.Idx.iftableifinerrors,
			Parameter.Iftable.Idx.iftableifinunknownprotos,
			Parameter.Iftable.Idx.iftableifoutoctets,
			Parameter.Iftable.Idx.iftableifoutucastpkts,
			Parameter.Iftable.Idx.iftableifoutdiscards,
			Parameter.Iftable.Idx.iftableifouterrors,
			Parameter.Iftable.Idx.iftableifinbitrate,
			Parameter.Iftable.Idx.iftableifoutbitrate,
			Parameter.Iftable.Idx.iftableifbandwidthutilization,
		};

		object[] ifTableColumns = protocol.GetColumns(Parameter.Iftable.tablePid, columnsToGet);

		this.Keys = (object[])ifTableColumns[0];
		this.Descriptions = (object[])ifTableColumns[1];
		this.Types = (object[])ifTableColumns[2];
		this.MTUs = (object[])ifTableColumns[3];
		this.Speeds = (object[])ifTableColumns[4];
		this.PhysAddress = (object[])ifTableColumns[5];
		this.AdminStatus = (object[])ifTableColumns[6];
		this.OperStatus = (object[])ifTableColumns[7];
		this.LastChange = (object[])ifTableColumns[8];
		this.InOctets = (object[])ifTableColumns[9];
		this.InUcastpkts = (object[])ifTableColumns[10];
		this.InDiscards = (object[])ifTableColumns[11];
		this.InErrors = (object[])ifTableColumns[12];
		this.InUnknownProtos = (object[])ifTableColumns[13];
		this.OutOctets = (object[])ifTableColumns[14];
		this.OutUcastpkts = (object[])ifTableColumns[15];
		this.OutDiscards = (object[])ifTableColumns[16];
		this.OutErrors = (object[])ifTableColumns[17];
		this.BitRateIn = (object[])ifTableColumns[18];
		this.BitRateOut = (object[])ifTableColumns[19];
		this.BandwidthUtilization = (object[])ifTableColumns[20];
	}

	public object[] AdminStatus { get; set; }

	public object[] BandwidthUtilization { get; set; }

	public object[] BitRateIn { get; set; }

	public object[] BitRateOut { get; set; }

	public object[] Descriptions { get; set; }

	public object[] InDiscards { get; set; }

	public object[] InErrors { get; set; }

	public object[] InOctets { get; set; }

	public object[] InUcastpkts { get; set; }

	public object[] InUnknownProtos { get; set; }

	public object[] LastChange { get; set; }

	public object[] MTUs { get; set; }

	public object[] OperStatus { get; set; }

	public object[] OutDiscards { get; set; }

	public object[] OutErrors { get; set; }

	public object[] OutOctets { get; set; }

	public object[] OutUcastpkts { get; set; }

	public object[] PhysAddress { get; set; }

	public object[] Speeds { get; set; }

	public object[] Types { get; set; }

	public object[] Keys { get; set; }
}

public class IfXTable
{
	public IfXTable(SLProtocol protocol)
	{
		uint[] columnsToGet = new uint[]
		{
			Parameter.Ifxtable.Idx.ifxtableifindex,
			Parameter.Ifxtable.Idx.ifxtableifname,
			Parameter.Ifxtable.Idx.ifxtableifinmulticastpkts,
			Parameter.Ifxtable.Idx.ifxtableifinbroadcastpkts,
			Parameter.Ifxtable.Idx.ifxtableifoutmulticastpkts,
			Parameter.Ifxtable.Idx.ifxtableifoutbroadcastpkts,
			Parameter.Ifxtable.Idx.ifxtableifhcinoctets,
			Parameter.Ifxtable.Idx.ifxtableifhcinucastpkts,
			Parameter.Ifxtable.Idx.ifxtableifhcinmulticastpkts,
			Parameter.Ifxtable.Idx.ifxtableifhcinbroadcastpkts,
			Parameter.Ifxtable.Idx.ifxtableifhcoutoctets,
			Parameter.Ifxtable.Idx.ifxtableifhcoutucastpkts,
			Parameter.Ifxtable.Idx.ifxtableifhcoutmulticastpkts,
			Parameter.Ifxtable.Idx.ifxtableifhcoutbroadcastpkts,
			Parameter.Ifxtable.Idx.ifxtableiflinkupdowntrapenable,
			Parameter.Ifxtable.Idx.ifxtableifhighspeed,
			Parameter.Ifxtable.Idx.ifxtableifpromiscuousmode,
			Parameter.Ifxtable.Idx.ifxtableifconnectorpresent,
			Parameter.Ifxtable.Idx.ifxtableifalias,
			Parameter.Ifxtable.Idx.ifxtableifcounterdiscontinuitytime,
			Parameter.Ifxtable.Idx.ifxtableifinbitrate,
			Parameter.Ifxtable.Idx.ifxtableifoutbitrate,
			Parameter.Ifxtable.Idx.ifxtableifbandwidthutilization,
		};

		object[] ifXTableColumns = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);

		this.Keys = (object[])ifXTableColumns[0];
		this.Name = (object[])ifXTableColumns[1];
		this.InMulticastPkts = (object[])ifXTableColumns[2];
		this.InBroadcastPkts = (object[])ifXTableColumns[3];
		this.OutMulticastPkts = (object[])ifXTableColumns[4];
		this.OutBroadcastPkts = (object[])ifXTableColumns[5];
		this.HcInOctets = (object[])ifXTableColumns[6];
		this.HcInUcastPkts = (object[])ifXTableColumns[7];
		this.HcInMulticastPkts = (object[])ifXTableColumns[8];
		this.HcInBroadcastPkts = (object[])ifXTableColumns[9];
		this.HcOutOctets = (object[])ifXTableColumns[10];
		this.HcOutUcastPkts = (object[])ifXTableColumns[11];
		this.HcOutMulticastPkts = (object[])ifXTableColumns[12];
		this.HcOutBroadcastPkts = (object[])ifXTableColumns[13];
		this.LinkUpDownTrapEnable = (object[])ifXTableColumns[14];
		this.HighSpeed = (object[])ifXTableColumns[15];
		this.PromiscuousMode = (object[])ifXTableColumns[16];
		this.ConnectorPresent = (object[])ifXTableColumns[17];
		this.Alias = (object[])ifXTableColumns[18];
		this.CounterDiscontinuitytime = (object[])ifXTableColumns[19];
		this.BitRateIn = (object[])ifXTableColumns[20];
		this.BitRateOut = (object[])ifXTableColumns[21];
		this.BandwidthUtilization = (object[])ifXTableColumns[22];
	}

	public object[] Alias { get; set; }

	public object[] BandwidthUtilization { get; set; }

	public object[] BitRateIn { get; set; }

	public object[] BitRateOut { get; set; }

	public object[] ConnectorPresent { get; set; }

	public object[] CounterDiscontinuitytime { get; set; }

	public object[] HcInBroadcastPkts { get; set; }

	public object[] HcInMulticastPkts { get; set; }

	public object[] HcInOctets { get; set; }

	public object[] HcInUcastPkts { get; set; }

	public object[] HcOutBroadcastPkts { get; set; }

	public object[] HcOutMulticastPkts { get; set; }

	public object[] HcOutOctets { get; set; }

	public object[] HcOutUcastPkts { get; set; }

	public object[] HighSpeed { get; set; }

	public object[] InBroadcastPkts { get; set; }

	public object[] Keys { get; set; }

	public object[] InMulticastPkts { get; set; }

	public object[] LinkUpDownTrapEnable { get; set; }

	public object[] Name { get; set; }

	public object[] OutBroadcastPkts { get; set; }

	public object[] OutMulticastPkts { get; set; }

	public object[] PromiscuousMode { get; set; }
}