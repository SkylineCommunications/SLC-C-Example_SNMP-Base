using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;

public class IfTable
{
    public IfTable(SLProtocol protocol)
    {
        var columnsToGet = new uint[]
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
            Parameter.Iftable.Idx.iftableunknownprotocolratein_1027,
        };

        var columns = protocol.GetColumns(Parameter.Iftable.tablePid, columnsToGet);

        Keys = (object[])columns[0];
        Descriptions = (object[])columns[1];
        Types = (object[])columns[2];
        Mtu = (object[])columns[3];
        Speeds = (object[])columns[4];
        PhysAddress = (object[])columns[5];
        AdminStatus = (object[])columns[6];
        OperStatus = (object[])columns[7];
        LastChange = (object[])columns[8];
        InOctets = (object[])columns[9];
        InUnicastPackets = (object[])columns[10];
        InDiscards = (object[])columns[11];
        InErrors = (object[])columns[12];
        InUnknownProtocols = (object[])columns[13];
        OutOctets = (object[])columns[14];
        OutUnicastPackets = (object[])columns[15];
        OutDiscards = (object[])columns[16];
        OutErrors = (object[])columns[17];
        BitRateIn = (object[])columns[18];
        BitRateOut = (object[])columns[19];
        BandwidthUtilization = (object[])columns[20];
        RateData = (object[])columns[21];
        UnicastRateIn = (object[])columns[22];
        UnicastRateOut = (object[])columns[23];
        DiscardRateIn = (object[])columns[24];
        DiscardRateOut = (object[])columns[25];
        ErrorRateIn = (object[])columns[26];
        ErrorRateOut = (object[])columns[27];
        UnknownProtocolRateIn = (object[])columns[28];
    }

    public object[] Keys { get; set; }

    public object[] Descriptions { get; set; }

    public object[] Types { get; set; }

    public object[] Mtu { get; set; }

    public object[] Speeds { get; set; }

    public object[] PhysAddress { get; set; }

    public object[] AdminStatus { get; set; }

    public object[] OperStatus { get; set; }

    public object[] LastChange { get; set; }

    public object[] InOctets { get; set; }

    public object[] InUnicastPackets { get; set; }

    public object[] InDiscards { get; set; }

    public object[] InErrors { get; set; }

    public object[] InUnknownProtocols { get; set; }

    public object[] OutOctets { get; set; }

    public object[] OutUnicastPackets { get; set; }

    public object[] OutDiscards { get; set; }

    public object[] OutErrors { get; set; }

    public object[] BitRateIn { get; set; }

    public object[] BitRateOut { get; set; }

    public object[] BandwidthUtilization { get; set; }

    public object[] RateData { get; set; }

    public object[] UnicastRateIn { get; set; }

    public object[] UnicastRateOut { get; set; }

    public object[] DiscardRateIn { get; set; }

    public object[] DiscardRateOut { get; set; }

    public object[] ErrorRateIn { get; set; }

    public object[] ErrorRateOut { get; set; }

    public object[] UnknownProtocolRateIn { get; set; }
}