using System;
using System.Collections.Generic;
using System.Linq;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;

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
                InterfacesQActionRow interfaceStatesTableRow = new InterfacesQActionRow();
                InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow = new InterfacesstatecountersrxQActionRow();
                InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow = new InterfacesstatecounterstxQActionRow();

                MergeFromSnmpIfTable(interfaceStatesTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, iftable, i);

                string key = Convert.ToString(iftable.Keys[i]);
                if (duplexStatusValues.TryGetValue(key, out int duplexState))
                {
                    interfaceStatesTableRow.Interfacesduplexstatus_2018 = duplexState;
                }
                else
                {
                    interfaceStatesTableRow.Interfacesduplexstatus_2018 = -1; // N/A
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
                    InterfacesQActionRow interfaceStatesTableRow = interfacesStateQActionRows.InterfacesRow;
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
            InterfacesQActionRow[] interfacesStateArr = new InterfacesQActionRow[count];
            InterfacesstatecountersrxQActionRow[] interfacesStateCountersRxArr = new InterfacesstatecountersrxQActionRow[count];
            InterfacesstatecounterstxQActionRow[] interfacesStateCountersTxArr = new InterfacesstatecounterstxQActionRow[count];

            for (int i = 0; i < count; i++)
            {
                interfacesStateArr[i] = x[i].InterfacesRow;
                interfacesStateCountersRxArr[i] = x[i].InterfacesCountersRxTableRow;
                interfacesStateCountersTxArr[i] = x[i].InterfacesCountersTxTableRows;
            }

            protocol.interfaces.FillArray(interfacesStateArr);
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

    private static void MergeFromSnmpIfTable(InterfacesQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfTable iftable, int getPosition)
    {
        // Keys
        string key = Convert.ToString(iftable.Keys[getPosition]);
        interfaceTableRow.Interfacesindex_2001 = key;
        interfaceCountersRxTableRow.Interfacesstatecountersrxindex_2101 = interfaceCountersRxTableRow.Interfacesstatecountersrxfktointerfaces_2109 = key;
        interfaceCountersTxTableRow.Interfacesstatecounterstxindex_2201 = interfaceCountersTxTableRow.Interfacesstatecounterstxfktointerfaces_2208 = key;

        // Interface States Table
        interfaceTableRow.Interfacestype_2002= Convert.ToDouble(iftable.Types[getPosition]);
        interfaceTableRow.Interfacesmtu_2003 = Convert.ToDouble(iftable.MTUs[getPosition]);
        interfaceTableRow.Interfacesdescription_2004 = Convert.ToString(iftable.Descriptions[getPosition]);
        interfaceTableRow.Interfacesadminstatus_2005 = Convert.ToDouble(iftable.AdminStatus[getPosition]);
        interfaceTableRow.Interfacesoperstatus_2006 = Convert.ToDouble(iftable.OperStatus[getPosition]);
        interfaceTableRow.Interfaceslastchange_2007 = Convert.ToDouble(iftable.LastChange[getPosition]);

        if (Convert.ToUInt32(iftable.Speeds[getPosition]) != MaxReportableIfSpeed)
        {
            // Speed in ifTable is expressed in bps, whereas speed in Interface table is expressed in Mbps.
            interfaceTableRow.Interfacesspeed_2016 = Convert.ToDouble(iftable.Speeds[getPosition]) / Math.Pow(10, 6);
        }

        if (Convert.ToDouble(iftable.Speeds[getPosition]) <= SpeedLimitForCounters)
        {
            // This means we should use the 32-bit versions.
            // Interface States Table
            interfaceTableRow.Interfacesrxoctets_2009 = Convert.ToDouble(iftable.InOctets[getPosition]);
            interfaceTableRow.Interfacestxoctets_2010 = Convert.ToDouble(iftable.OutOctets[getPosition]);

            //// Interface States Table - Calculated Values
            double dBitRateIn = Convert.ToDouble(iftable.BitRateIn[getPosition]);
            interfaceTableRow.Interfacesrxbitrate_2013 = dBitRateIn >= 0 ? dBitRateIn / Math.Pow(10, 6) : -1;    // bps -> Mbps

            double dBitRateOut = Convert.ToDouble(iftable.BitRateOut[getPosition]);
            interfaceTableRow.Interfacestxbitrate_2014 = dBitRateOut >= 0 ? dBitRateOut / Math.Pow(10, 6) : -1; // bps -> Mbps

            interfaceTableRow.Interfacesbandwidthutilization_2017 = Convert.ToDouble(iftable.BandwidthUtilization[getPosition]);

            // Interface Counters RX Table
            interfaceCountersRxTableRow.Interfacesstatecountersrxinunicastrate_2103 = Convert.ToDouble(iftable.UniCastRateIn[getPosition]);
            interfaceCountersRxTableRow.Interfacesstatecountersrxindiscardsrate_2106 = Convert.ToDouble(iftable.DiscardRateIn[getPosition]);
            interfaceCountersRxTableRow.Interfacesstatecountersrxinerrorsrate_2107 = Convert.ToDouble(iftable.ErrorRateIn[getPosition]);
            interfaceCountersRxTableRow.Interfacesstatecountersrxinunknownprotosrate_2108 = Convert.ToDouble(iftable.UnknownProtosRateIn[getPosition]);

            // Interface Counters TX Table
            interfaceCountersTxTableRow.Interfacesstatecounterstxoutunicastrate_2203 = Convert.ToDouble(iftable.UniCastRateOut[getPosition]);
            interfaceCountersTxTableRow.Interfacesstatecounterstxoutdiscardsrate_2206 = Convert.ToDouble(iftable.DiscardRateOut[getPosition]);
            interfaceCountersTxTableRow.Interfacesstatecounterstxouterrorsrate_2207 = Convert.ToDouble(iftable.ErrorRateOut[getPosition]);
        }
    }

    private static void MergeFromSnmpIfXTable(InterfacesQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfXTable ifxtable, int getPosition)
    {
        interfaceTableRow.Interfaceslogical_2008 = ifxtable.ConnectorPresent[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.ConnectorPresent[getPosition]);
        interfaceTableRow.Interfaceslastclear_2011 = ifxtable.CounterDiscontinuitytime[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.CounterDiscontinuitytime[getPosition]) / 100;
        interfaceTableRow.Interfacesuserdescription_2015 = Convert.ToString(ifxtable.Alias[getPosition]);
        interfaceTableRow.Interfacespromiscuousmode_2019 = ifxtable.PromiscuousMode[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.PromiscuousMode[getPosition]);
        interfaceTableRow.Interfaceslinkupdowntrap_2020 = Convert.ToDouble(ifxtable.LinkUpDownTrapEnable[getPosition]);

        if (ifxtable.HighSpeed[getPosition] != null)
        {
            interfaceTableRow.Interfacesspeed_2016 = Convert.ToDouble(ifxtable.HighSpeed[getPosition]);
        }

        if (interfaceTableRow.Interfacesrxoctets_2009 == null)
        {
            Use64BitCounters(interfaceTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, ifxtable, getPosition);
        }
        else
        {
            Use32BitCounters(interfaceTableRow, interfaceCountersRxTableRow, interfaceCountersTxTableRow, ifxtable, getPosition);
        }
    }

    private static void Use32BitCounters(InterfacesQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfXTable ifxtable, int getPosition)
    {
        // Interface Counters RX Table
        interfaceCountersRxTableRow.Interfacesstatecountersrxinbroadcastrate_2104 = ifxtable.BroadcastRateIn[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.BroadcastRateIn[getPosition]);
        interfaceCountersRxTableRow.Interfacesstatecountersrxinmulticastrate_2105 = ifxtable.MulticastRateIn[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.MulticastRateIn[getPosition]);

        // Interface Counters TX Table
        interfaceCountersTxTableRow.Interfacesstatecounterstxoutbroadcastrate_2204 = ifxtable.BroadcastRateOut[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.BroadcastRateOut[getPosition]);
        interfaceCountersTxTableRow.Interfacesstatecounterstxoutmulticastrate_2205 = ifxtable.MulticastRateOut[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.MulticastRateOut[getPosition]);

        double dBitrateIn = Convert.ToDouble(ifxtable.HCBitRateIn[getPosition]);
        if (dBitrateIn < -1)
        {
            // Indication of discontinuity times, need to set values to N/A
            interfaceTableRow.Interfacesrxbitrate_2013 = -1;
            interfaceTableRow.Interfacestxbitrate_2014 = -1;
            interfaceTableRow.Interfacesbandwidthutilization_2017 = -1;
        }
    }

    private static void Use64BitCounters(InterfacesQActionRow interfaceTableRow, InterfacesstatecountersrxQActionRow interfaceCountersRxTableRow, InterfacesstatecounterstxQActionRow interfaceCountersTxTableRow, IfXTable ifxtable, int getPosition)
    {
        // Interface States Table
        interfaceTableRow.Interfacesrxoctets_2009 = ifxtable.HcInOctets[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcInOctets[getPosition]);
        interfaceTableRow.Interfacestxoctets_2010 = ifxtable.HcOutOctets[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HcOutOctets[getPosition]);
        interfaceTableRow.Interfacesbandwidthutilization_2017 = Convert.ToDouble(ifxtable.BandwidthUtilization[getPosition]);

        double bitrateIn = Convert.ToDouble(ifxtable.HCBitRateIn[getPosition]);
        interfaceTableRow.Interfacesrxbitrate_2013 = bitrateIn >= 0 ? bitrateIn / Math.Pow(10, 6) : -1;      // bps -> Mbps

        double bitrateOut = Convert.ToDouble(ifxtable.HCBitRateOut[getPosition]);
        interfaceTableRow.Interfacestxbitrate_2014 = bitrateOut >= 0 ? bitrateOut / Math.Pow(10, 6) : -1;   // bps -> Mbps

        // Interface Counters RX Table
        interfaceCountersRxTableRow.Interfacesstatecountersrxinunicastrate_2103 = ifxtable.HCUnicastRateIn[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HCUnicastRateIn[getPosition]);
        interfaceCountersRxTableRow.Interfacesstatecountersrxinbroadcastrate_2104 = ifxtable.HCBroadcastRateIn[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HCBroadcastRateIn[getPosition]);
        interfaceCountersRxTableRow.Interfacesstatecountersrxinmulticastrate_2105 = ifxtable.HCMulticastRateIn[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HCMulticastRateIn[getPosition]);

        // Interface Counters TX Table
        interfaceCountersTxTableRow.Interfacesstatecounterstxoutunicastrate_2203 = ifxtable.HCUnicastRateOut[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HCUnicastRateOut[getPosition]);
        interfaceCountersTxTableRow.Interfacesstatecounterstxoutbroadcastrate_2204 = ifxtable.HCBroadcastRateOut[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HCBroadcastRateOut[getPosition]);
        interfaceCountersTxTableRow.Interfacesstatecounterstxoutmulticastrate_2205 = ifxtable.HCMulticastRateOut[getPosition] == null ? -1 : Convert.ToDouble(ifxtable.HCMulticastRateOut[getPosition]);
    }
}

public class InterfacesStateQActionRows
{
    public InterfacesStateQActionRows(InterfacesQActionRow interfacesStateTableRow, InterfacesstatecountersrxQActionRow interfacesCountersRxTableRow, InterfacesstatecounterstxQActionRow interfacesCountersTxTableRows)
    {
        InterfacesRow = interfacesStateTableRow;
        InterfacesCountersRxTableRow = interfacesCountersRxTableRow;
        InterfacesCountersTxTableRows = interfacesCountersTxTableRows;
    }

    public InterfacesQActionRow InterfacesRow { get; set; }

    public InterfacesstatecountersrxQActionRow InterfacesCountersRxTableRow { get; set; }

    public InterfacesstatecounterstxQActionRow InterfacesCountersTxTableRows { get; set; }
}

public class IfTable
{
    public IfTable(SLProtocol protocol)
    {
        uint[] columnsToGet = new uint[]
        {
            Parameter.Iftable.Idx.iftableindex_1001,
            Parameter.Iftable.Idx.iftabledescr_1002,
            Parameter.Iftable.Idx.iftabletype_1003,
            Parameter.Iftable.Idx.iftablemtu_1004,
            Parameter.Iftable.Idx.iftablespeed_1005,
            Parameter.Iftable.Idx.iftablephysaddress_1006,
            Parameter.Iftable.Idx.iftableadminstatus_1007,
            Parameter.Iftable.Idx.iftableoperstatus_1008,
            Parameter.Iftable.Idx.iftablelastchange_1009,
            Parameter.Iftable.Idx.iftableoctetsin_1010,
            Parameter.Iftable.Idx.iftableucastpktsin_1012,
            Parameter.Iftable.Idx.iftablediscardsin_1014,
            Parameter.Iftable.Idx.iftableerrorsin_1016,
            Parameter.Iftable.Idx.iftableunknownprotosin_1018,
            Parameter.Iftable.Idx.iftableoctetsout_1011,
            Parameter.Iftable.Idx.iftableucastpktsout_1013,
            Parameter.Iftable.Idx.iftablediscardsout_1015,
            Parameter.Iftable.Idx.iftableerrorsout_1017,
            Parameter.Iftable.Idx.iftablebitratein_1019,
            Parameter.Iftable.Idx.iftablebitrateout_1020,
            Parameter.Iftable.Idx.iftablebandwidthutilization_1028,
            Parameter.Iftable.Idx.iftableratesdata_1029,
            Parameter.Iftable.Idx.iftableunicastratein_1021,
            Parameter.Iftable.Idx.iftableunicastrateout_1022,
            Parameter.Iftable.Idx.iftablediscardratein_1023,
            Parameter.Iftable.Idx.iftablediscardrateout_1024,
            Parameter.Iftable.Idx.iftableerrorratein_1025,
            Parameter.Iftable.Idx.iftableerrorrateout_1026,
            Parameter.Iftable.Idx.iftableunknownprotosratein_1027,
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
        this.RateData = (object[])ifTableColumns[21];
        this.UniCastRateIn = (object[])ifTableColumns[22];
        this.UniCastRateOut = (object[])ifTableColumns[23];
        this.DiscardRateIn = (object[])ifTableColumns[24];
        this.DiscardRateOut = (object[])ifTableColumns[25];
        this.ErrorRateIn = (object[])ifTableColumns[26];
        this.ErrorRateOut = (object[])ifTableColumns[27];
        this.UnknownProtosRateIn = (object[])ifTableColumns[28];
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

    public object[] RateData { get; set; }

    public object[] UniCastRateIn { get; set; }

    public object[] UniCastRateOut { get; set; }

    public object[] DiscardRateIn { get; set; }

    public object[] DiscardRateOut { get; set; }

    public object[] ErrorRateIn { get; set; }

    public object[] ErrorRateOut { get; set; }

    public object[] UnknownProtosRateIn { get; set; }
}

public class IfXTable
{
    public IfXTable(SLProtocol protocol)
    {
        uint[] columnsToGet = new uint[]
        {
            Parameter.Ifxtable.Idx.ifxtableindex_1101,
            Parameter.Ifxtable.Idx.ifxtablename_1102,
            Parameter.Ifxtable.Idx.ifxtablemulticastpktsin_1103,
            Parameter.Ifxtable.Idx.ifxtablebroadcastpktsin_1105,
            Parameter.Ifxtable.Idx.ifxtablemulticastpktsout_1104,
            Parameter.Ifxtable.Idx.ifxtablebroadcastpktsout_1106,
            Parameter.Ifxtable.Idx.ifxtablehcoctetsin_1107,
            Parameter.Ifxtable.Idx.ifxtablehcucastpktsin_1109,
            Parameter.Ifxtable.Idx.ifxtablehcmulticastpktsin_1111,
            Parameter.Ifxtable.Idx.ifxtablehcbroadcastpktsin_1113,
            Parameter.Ifxtable.Idx.ifxtablehcoctetsout_1108,
            Parameter.Ifxtable.Idx.ifxtablehcucastpktsout_1110,
            Parameter.Ifxtable.Idx.ifxtablehcmulticastpktsout_1112,
            Parameter.Ifxtable.Idx.ifxtablehcbroadcastpktsout_1114,
            Parameter.Ifxtable.Idx.ifxtablelinkupdowntrapenable_1115,
            Parameter.Ifxtable.Idx.ifxtablehighspeed_1116,
            Parameter.Ifxtable.Idx.ifxtablepromiscuousmode_1117,
            Parameter.Ifxtable.Idx.ifxtableconnectorpresent_1118,
            Parameter.Ifxtable.Idx.ifxtablealias_1119,
            Parameter.Ifxtable.Idx.ifxtablecounterdiscontinuitytime_1120,
            Parameter.Ifxtable.Idx.ifxtablebitratein_1121,
            Parameter.Ifxtable.Idx.ifxtablebitrateout_1122,
            Parameter.Ifxtable.Idx.ifxtablebandwidthutilization_1123,
            Parameter.Ifxtable.Idx.ifxtableratesdata_1124,
            Parameter.Ifxtable.Idx.ifxtablemulticastratein_1125,
            Parameter.Ifxtable.Idx.ifxtablemulticastrateout_1126,
            Parameter.Ifxtable.Idx.ifxtablebroadcastratein_1127,
            Parameter.Ifxtable.Idx.ifxtablebroadcastrateout_1128,
            Parameter.Ifxtable.Idx.ifxtablehcucastratein_1129,
            Parameter.Ifxtable.Idx.ifxtablehcucastrateout_1130,
            Parameter.Ifxtable.Idx.ifxtablehcmulticastratein_1131,
            Parameter.Ifxtable.Idx.ifxtablehcmulticastrateout_1132,
            Parameter.Ifxtable.Idx.ifxtablehcbroadcastratein_1133,
            Parameter.Ifxtable.Idx.ifxtablehcbroadcastrateout_1134,
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
        this.HCBitRateIn = (object[])ifXTableColumns[20];
        this.HCBitRateOut = (object[])ifXTableColumns[21];
        this.BandwidthUtilization = (object[])ifXTableColumns[22];
        this.RateDate = (object[])ifXTableColumns[23];
        this.MulticastRateIn = (object[])ifXTableColumns[24];
        this.MulticastRateOut = (object[])ifXTableColumns[25];
        this.BroadcastRateIn = (object[])ifXTableColumns[26];
        this.BroadcastRateOut = (object[])ifXTableColumns[27];
        this.HCUnicastRateIn = (object[])ifXTableColumns[28];
        this.HCUnicastRateOut = (object[])ifXTableColumns[29];
        this.HCMulticastRateIn = (object[])ifXTableColumns[30];
        this.HCMulticastRateOut = (object[])ifXTableColumns[31];
        this.HCBroadcastRateIn = (object[])ifXTableColumns[32];
        this.HCBroadcastRateOut = (object[])ifXTableColumns[33];
    }

    public object[] Alias { get; set; }

    public object[] BandwidthUtilization { get; set; }

    public object[] MulticastRateIn { get; set; }

    public object[] MulticastRateOut { get; set; }

    public object[] BroadcastRateIn { get; set; }

    public object[] BroadcastRateOut { get; set; }

    public object[] HCBitRateIn { get; set; }

    public object[] HCBitRateOut { get; set; }

    public object[] HCUnicastRateIn { get; set; }

    public object[] HCUnicastRateOut { get; set; }

    public object[] HCMulticastRateIn { get; set; }

    public object[] HCMulticastRateOut { get; set; }

    public object[] HCBroadcastRateIn { get; set; }

    public object[] HCBroadcastRateOut { get; set; }

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

    public object[] RateDate { get; set; }
}