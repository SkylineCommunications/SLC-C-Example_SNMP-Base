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
			Dictionary<string, InterfacesQActionRow> interfaceTableRows = new Dictionary<string, InterfacesQActionRow>();

			Dictionary<string, int> duplexStatusValues = GetDuplexStatus(protocol);

			// ifTable
			IfTable iftable = new IfTable(protocol);
			for (int i = 0; i < iftable.Keys.Length; i++)
			{
				InterfacesQActionRow interfaceTableRow = new InterfacesQActionRow(); // TODO: Delete
				InterfacesstateQActionRow interfaceStatesTableRow = new InterfacesstateQActionRow();
				InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow = new InterfacesstatecountersrxQActionRow();
				InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow = new InterfacesstatecounterstxQActionRow();

				MergeFromSnmpIfTable(interfaceStatesTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, iftable, i);

				string key = Convert.ToString(iftable.Keys[i]);
				if (duplexStatusValues.TryGetValue(key, out int duplexState))
				{
					interfaceTableRow.Interfacesduplexstatus = duplexState;
				}
				else
				{
					interfaceTableRow.Interfacesduplexstatus = -1; // N/A
				}

				interfaceTableRows.Add(key, interfaceTableRow);
			}

			// ifXTable.
			IfXTable ifxtable = new IfXTable(protocol);
			for (int i = 0; i < ifxtable.Keys.Length; i++)
			{
				string key = Convert.ToString(ifxtable.Keys[i]);

				if (interfaceTableRows.TryGetValue(key, out InterfacesQActionRow interfaceTableRow))
				{
					MergeFromSnmpIfXTable(interfaceTableRow, ifxtable, i);
				}
			}

			var rows = interfaceTableRows.Values.ToArray();
			protocol.interfaces.FillArray(rows);
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

	// TODO: Delete
	private static void MergeFromSnmpIfTable(InterfacesQActionRow interfaceTableRow, IfTable iftable, int getPosition)
	{
		interfaceTableRow.Interfacesindex = Convert.ToString(iftable.Keys[getPosition]);
		interfaceTableRow.Interfacesdescr = Convert.ToString(iftable.Descriptions[getPosition]);
		interfaceTableRow.Interfacestype = Convert.ToDouble(iftable.Types[getPosition]);
		interfaceTableRow.Interfacesmtu = Convert.ToDouble(iftable.MTUs[getPosition]);
		interfaceTableRow.Interfacesphysaddress = Convert.ToString(iftable.PhysAddress[getPosition]);
		interfaceTableRow.Interfacesadminstatus = Convert.ToDouble(iftable.AdminStatus[getPosition]);
		interfaceTableRow.Interfacesoperstatus = Convert.ToDouble(iftable.OperStatus[getPosition]);
		interfaceTableRow.Interfaceslastchange = Convert.ToDouble(iftable.LastChange[getPosition]);

		interfaceTableRow.Interfacesindiscards = Convert.ToDouble(iftable.InDiscards[getPosition]);
		interfaceTableRow.Interfacesinerrors = Convert.ToDouble(iftable.InErrors[getPosition]);
		interfaceTableRow.Interfacesinunknownprotos = Convert.ToDouble(iftable.InUnknownProtos[getPosition]);

		interfaceTableRow.Interfacesoutdiscards = Convert.ToDouble(iftable.OutDiscards[getPosition]);
		interfaceTableRow.Interfacesouterrors = Convert.ToDouble(iftable.OutErrors[getPosition]);

		if (Convert.ToUInt32(iftable.Speeds[getPosition]) != MaxReportableIfSpeed)
		{
			// Speed in ifTable is expressed in bps, whereas speed in Interface table is expressed in Mbps.
			interfaceTableRow.Interfacesspeed = Convert.ToDouble(iftable.Speeds[getPosition]) / Math.Pow(10, 6);
		}

		if (Convert.ToDouble(iftable.Speeds[getPosition]) <= SpeedLimitForCounters)
		{
			// This means we should use the 32-bit versions.
			interfaceTableRow.Interfacesinoctets = Convert.ToDouble(iftable.InOctets[getPosition]);
			interfaceTableRow.Interfacesinucastpkts = Convert.ToDouble(iftable.InUcastpkts[getPosition]);
			interfaceTableRow.Interfacesoutoctets = Convert.ToDouble(iftable.OutOctets[getPosition]);
			interfaceTableRow.Interfacesoutucastpkts = Convert.ToDouble(iftable.OutUcastpkts[getPosition]);

			interfaceTableRow.Interfacesbandwidthutilization = Convert.ToDouble(iftable.BandwidthUtilization[getPosition]);

			double dBitRateIn = Convert.ToDouble(iftable.BitRateIn[getPosition]);
			interfaceTableRow.Interfacesinbitrate = dBitRateIn >= 0 ? dBitRateIn / Math.Pow(10, 6) : -1;	// bps -> Mbps

			double dBitRateOut = Convert.ToDouble(iftable.BitRateOut[getPosition]);
			interfaceTableRow.Interfacesoutbitrate = dBitRateOut >= 0 ? dBitRateOut / Math.Pow(10, 6) : -1; // bps -> Mbps
		}
	}

	// TODO: remove commented
	private static void MergeFromSnmpIfTable(InterfacesstateQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfTable iftable, int getPosition)
	{
		// Interface States Table
		interfaceTableRow.Interfacesstatetype_2002 = Convert.ToDouble(iftable.Types[getPosition]);
		interfaceTableRow.Interfacesstatemtu_2003 = Convert.ToDouble(iftable.MTUs[getPosition]);

		interfaceTableRow.Interfacesstatedescription_2005 = Convert.ToString(iftable.Descriptions[getPosition]);

		interfaceTableRow.Interfacesstateifindex_2007 = Convert.ToString(iftable.Keys[getPosition]);
		interfaceTableRow.Interfacesstateadminstatus_2008 = Convert.ToDouble(iftable.AdminStatus[getPosition]);
		interfaceTableRow.Interfacesstateoperstatus_2009 = Convert.ToDouble(iftable.OperStatus[getPosition]);
		interfaceTableRow.Interfacesstatelastchange_2010 = Convert.ToDouble(iftable.LastChange[getPosition]);
		interfaceTableRow.Interfacesstateinoctets_2012 = Convert.ToDouble(iftable.InOctets[getPosition]);
		interfaceTableRow.Interfacesstateoutoctets_2013 = Convert.ToDouble(iftable.OutOctets[getPosition]);

		// Interface Counters RX Table
		interfaceCountersRxTableRow.Interfacesstatecountersrxinoctets_2102 = Convert.ToDouble(iftable.InOctets[getPosition]);
		interfaceCountersRxTableRow.Interfacesstatecountersrxindiscards_2107 = Convert.ToDouble(iftable.InDiscards[getPosition]);
		interfaceCountersRxTableRow.Interfacesstatecountersrxinerrors_2108 = Convert.ToDouble(iftable.InErrors[getPosition]);
		interfaceCountersRxTableRow.Interfacesstatecountersrxinunknownprotos_2109 = Convert.ToDouble(iftable.InUnknownProtos[getPosition]);

		// Interface Counters TX Table
		interfaceCountersTxTableRow.Interfacesstatecounterstxoutoctets_2202 = Convert.ToDouble(iftable.OutOctets[getPosition]);
		interfaceCountersTxTableRow.Interfacesstatecounterstxoutdiscards_2207 = Convert.ToDouble(iftable.OutDiscards[getPosition]);
		interfaceCountersTxTableRow.Interfacesstatecounterstxouterrors_2208 = Convert.ToDouble(iftable.OutErrors[getPosition]);

		if (Convert.ToUInt32(iftable.Speeds[getPosition]) != MaxReportableIfSpeed)
		{
			// Speed in ifTable is expressed in bps, whereas speed in Interface table is expressed in Mbps.
			interfaceTableRow.Interfacesstatespeed_2022 = Convert.ToDouble(iftable.Speeds[getPosition]) / Math.Pow(10, 6);
		}

		if (Convert.ToDouble(iftable.Speeds[getPosition]) <= SpeedLimitForCounters)
		{
			// This means we should use the 32-bit versions.
			// Interface States Table
			interfaceTableRow.Interfacesstateinoctets_2012 = Convert.ToDouble(iftable.InOctets[getPosition]);
			interfaceTableRow.Interfacesstateoutoctets_2013 = Convert.ToDouble(iftable.OutOctets[getPosition]);

			//// Interface States Table - Calculated Values
			double dBitRateIn = Convert.ToDouble(iftable.BitRateIn[getPosition]);
			interfaceTableRow.Interfacesstateinbitrate_2017 = dBitRateIn >= 0 ? dBitRateIn / Math.Pow(10, 6) : -1;    // bps -> Mbps

			double dBitRateOut = Convert.ToDouble(iftable.BitRateOut[getPosition]);
			interfaceTableRow.Interfacesstateoutbitrate_2018 = dBitRateOut >= 0 ? dBitRateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

			interfaceTableRow.Interfacesbandwidthutilization_2023 = Convert.ToDouble(iftable.BandwidthUtilization[getPosition]);

			// Interface Counters RX Table
			interfaceCountersRxTableRow.Interfacesstatecountersrxinunicastpckts_2104 = Convert.ToDouble(iftable.InUcastpkts[getPosition]);

			// Interface Counters TX Table
			interfaceCountersTxTableRow.Interfacesstatecounterstxoutunicastrate_2213 = Convert.ToDouble(iftable.OutUcastpkts[getPosition]);
		}
	}

	// TODO: Delete
	private static void MergeFromSnmpIfXTable(InterfacesQActionRow interfaceTableRow, IfXTable ifxtable, int getPosition)
	{
		interfaceTableRow.Interfacespromiscuousmode = ifxtable.PromiscuousMode[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.PromiscuousMode[getPosition]);
		interfaceTableRow.Interfacesphysicalconnector = ifxtable.ConnectorPresent[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.ConnectorPresent[getPosition]);
		interfaceTableRow.Interfacesalias = Convert.ToString(ifxtable.Alias[getPosition]);
		interfaceTableRow.Interfacescounterdiscontinuitytime = Convert.ToDouble(ifxtable.CounterDiscontinuitytime[getPosition]) / 100;
		interfaceTableRow.Interfaceslinkupdowntrapenable = Convert.ToDouble(ifxtable.LinkUpDownTrapEnable[getPosition]);

		if (interfaceTableRow.Interfacesspeed == null)
		{
			interfaceTableRow.Interfacesspeed = ifxtable.HighSpeed[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HighSpeed[getPosition]);
		}

		if (interfaceTableRow.Interfacesinoctets == null)
		{
			Use64BitCounters(interfaceTableRow, ifxtable, getPosition);
		}
		else
		{
			Use32BitCounters(interfaceTableRow, ifxtable, getPosition);
		}
	}

	private static void MergeFromSnmpIfXTable(InterfacesstateQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, , IfXTable ifxtable, int getPosition)
	{
		interfaceTableRow.Interfacesstatelogical_2011 = ifxtable.ConnectorPresent[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.ConnectorPresent[getPosition]);
		interfaceTableRow.Interfacesstatelastclear_2015 = Convert.ToDouble(ifxtable.CounterDiscontinuitytime[getPosition]) / 100;

		interfaceTableRow.Interfacespromiscuousmode = ifxtable.PromiscuousMode[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.PromiscuousMode[getPosition]);
		
		interfaceTableRow.Interfacesalias = Convert.ToString(ifxtable.Alias[getPosition]);
		
		interfaceTableRow.Interfaceslinkupdowntrapenable = Convert.ToDouble(ifxtable.LinkUpDownTrapEnable[getPosition]);

		if (interfaceTableRow.Interfacesspeed == null)
		{
			interfaceTableRow.Interfacesspeed = ifxtable.HighSpeed[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HighSpeed[getPosition]);
		}

		if (interfaceTableRow.Interfacesinoctets == null)
		{
			Use64BitCounters(interfaceTableRow, ifxtable, getPosition);
		}
		else
		{
			Use32BitCounters(interfaceTableRow, ifxtable, getPosition);
		}
	}

	private static void Use32BitCounters(InterfacesQActionRow interfaceTableRow, IfXTable ifxtable, int getPosition)
	{
		interfaceTableRow.Interfacesinmulticastpkts = ifxtable.InMulticastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.InMulticastPkts[getPosition]);
		interfaceTableRow.Interfacesinbroadcastpkts = ifxtable.InBroadcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.InBroadcastPkts[getPosition]);

		interfaceTableRow.Interfacesoutmulticastpkts = ifxtable.OutMulticastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.OutMulticastPkts[getPosition]);
		interfaceTableRow.Interfacesoutbroadcastpkts = ifxtable.OutBroadcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.OutBroadcastPkts[getPosition]);

		double dBitrateIn = Convert.ToDouble(ifxtable.BitRateIn[getPosition]);
		if (dBitrateIn < -1)
		{
			// Indication of discontinuity times, need to set values to N/A
			interfaceTableRow.Interfacesinbitrate = -1;
			interfaceTableRow.Interfacesoutbitrate = -1;
			interfaceTableRow.Interfacesbandwidthutilization = -1;
		}
	}

	private static void Use64BitCounters(InterfacesQActionRow interfaceTableRow, IfXTable ifxtable, int getPosition)
	{
		interfaceTableRow.Interfacesinoctets = ifxtable.HcInOctets[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInOctets[getPosition]);
		interfaceTableRow.Interfacesinucastpkts = ifxtable.HcInUcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInUcastPkts[getPosition]);
		interfaceTableRow.Interfacesinmulticastpkts = ifxtable.HcInMulticastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInMulticastPkts[getPosition]);
		interfaceTableRow.Interfacesinbroadcastpkts = ifxtable.HcInBroadcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInBroadcastPkts[getPosition]);

		interfaceTableRow.Interfacesoutoctets = ifxtable.HcOutOctets[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutOctets[getPosition]);
		interfaceTableRow.Interfacesoutucastpkts = ifxtable.HcOutUcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutUcastPkts[getPosition]);
		interfaceTableRow.Interfacesoutmulticastpkts = ifxtable.HcOutMulticastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutMulticastPkts[getPosition]);
		interfaceTableRow.Interfacesoutbroadcastpkts = ifxtable.HcOutBroadcastPkts[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutBroadcastPkts[getPosition]);

		interfaceTableRow.Interfacesbandwidthutilization = Convert.ToDouble(ifxtable.BandwidthUtilization[getPosition]);

		double bitrateIn = Convert.ToDouble(ifxtable.BitRateIn[getPosition]);
		interfaceTableRow.Interfacesinbitrate = bitrateIn >= 0 ? bitrateIn / Math.Pow(10, 6) : -1;		// bps -> Mbps

		double bitrateOut = Convert.ToDouble(ifxtable.BitRateOut[getPosition]);
		interfaceTableRow.Interfacesoutbitrate = bitrateOut >= 0 ? bitrateOut / Math.Pow(10, 6) : -1;   // bps -> Mbps
	}
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