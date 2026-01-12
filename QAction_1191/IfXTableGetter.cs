using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;

public class IfXTableGetter
{
	public IfXTableGetter(SLProtocol protocol)
	{
		var columnsToGet = new uint[]
		{
			Parameter.Ifxtable.Idx.ifxtable_ifindex_1101,
			Parameter.Ifxtable.Idx.ifxtable_ifname_1102,
			Parameter.Ifxtable.Idx.ifxtable_ifmulticastpktsin_1103,
			Parameter.Ifxtable.Idx.ifxtable_ifbroadcastpktsin_1105,
			Parameter.Ifxtable.Idx.ifxtable_ifmulticastpktsout_1104,
			Parameter.Ifxtable.Idx.ifxtable_ifbroadcastpktsout_1106,
			Parameter.Ifxtable.Idx.ifxtable_ifhcoctetsin_1107,
			Parameter.Ifxtable.Idx.ifxtable_ifhcucastpktsin_1109,
			Parameter.Ifxtable.Idx.ifxtable_ifhcmulticastpktsin_1111,
			Parameter.Ifxtable.Idx.ifxtable_ifhcbroadcastpktsin_1113,
			Parameter.Ifxtable.Idx.ifxtable_ifhcoctetsout_1108,
			Parameter.Ifxtable.Idx.ifxtable_ifhcucastpktsout_1110,
			Parameter.Ifxtable.Idx.ifxtable_ifhcmulticastpktsout_1112,
			Parameter.Ifxtable.Idx.ifxtable_ifhcbroadcastpktsout_1114,
			Parameter.Ifxtable.Idx.ifxtable_ifhighspeed_1116,
			Parameter.Ifxtable.Idx.ifxtable_ifpromiscuousmode_1117,
			Parameter.Ifxtable.Idx.ifxtable_ifalias_1119,
			Parameter.Ifxtable.Idx.ifxtable_ifcounterdiscontinuitytime_1120,

			Parameter.Ifxtable.Idx.ifxtable_bitratein_1121,
			Parameter.Ifxtable.Idx.ifxtable_bitrateout_1122,
			Parameter.Ifxtable.Idx.ifxtable_bandwidthutilization_1123,
			Parameter.Ifxtable.Idx.ifxtable_ratesdata_1124,
			Parameter.Ifxtable.Idx.ifxtable_multicastratein_1125,
			Parameter.Ifxtable.Idx.ifxtable_multicastrateout_1126,
			Parameter.Ifxtable.Idx.ifxtable_broadcastratein_1127,
			Parameter.Ifxtable.Idx.ifxtable_broadcastrateout_1128,
			Parameter.Ifxtable.Idx.ifxtable_hcucastratein_1129,
			Parameter.Ifxtable.Idx.ifxtable_hcucastrateout_1130,
			Parameter.Ifxtable.Idx.ifxtable_hcmulticastratein_1131,
			Parameter.Ifxtable.Idx.ifxtable_hcmulticastrateout_1132,
			Parameter.Ifxtable.Idx.ifxtable_hcbroadcastratein_1133,
			Parameter.Ifxtable.Idx.ifxtable_hcbroadcastrateout_1134,

			Parameter.Ifxtable.Idx.ifxtable_rxbandwidthutilization_1135,
			Parameter.Ifxtable.Idx.ifxtable_txbandwidthutilization_1136,
		};

		var columns = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);

		Keys = (object[])columns[0];
		Name = (object[])columns[1];
		InMulticastPackets = (object[])columns[2];
		InBroadcastPackets = (object[])columns[3];
		OutMulticastPackets = (object[])columns[4];
		OutBroadcastPackets = (object[])columns[5];
		HcInOctets = (object[])columns[6];
		HcInUnicastPackets = (object[])columns[7];
		HcInMulticastPackets = (object[])columns[8];
		HcInBroadcastPackets = (object[])columns[9];
		HcOutOctets = (object[])columns[10];
		HcOutUnicastPackets = (object[])columns[11];
		HcOutMulticastPackets = (object[])columns[12];
		HcOutBroadcastPackets = (object[])columns[13];

		HighSpeed = (object[])columns[14];
		PromiscuousMode = (object[])columns[15];

		Alias = (object[])columns[16];
		CounterDiscontinuityTime = (object[])columns[17];
		HcBitRateIn = (object[])columns[18];
		HcBitRateOut = (object[])columns[19];
		BandwidthUtilization = (object[])columns[20];
		RateData = (object[])columns[21];
		MulticastRateIn = (object[])columns[22];
		MulticastRateOut = (object[])columns[23];
		BroadcastRateIn = (object[])columns[24];
		BroadcastRateOut = (object[])columns[25];
		HcUnicastRateIn = (object[])columns[26];
		HcUnicastRateOut = (object[])columns[27];
		HcMulticastRateIn = (object[])columns[28];
		HcMulticastRateOut = (object[])columns[29];
		HcBroadcastRateIn = (object[])columns[30];
		HcBroadcastRateOut = (object[])columns[31];

		RxBandwidthUtilization = (object[])columns[32];
		TxBandwidthUtilization = (object[])columns[33];
    }

	public object[] Keys { get; set; }

	public object[] Name { get; set; }

	public object[] InMulticastPackets { get; set; }

	public object[] InBroadcastPackets { get; set; }

	public object[] OutMulticastPackets { get; set; }

	public object[] OutBroadcastPackets { get; set; }

	public object[] HcInOctets { get; set; }

	public object[] HcInUnicastPackets { get; set; }

	public object[] HcInMulticastPackets { get; set; }

	public object[] HcInBroadcastPackets { get; set; }

	public object[] HcOutOctets { get; set; }

	public object[] HcOutUnicastPackets { get; set; }

	public object[] HcOutMulticastPackets { get; set; }

	public object[] HcOutBroadcastPackets { get; set; }

	public object[] HighSpeed { get; set; }

	public object[] PromiscuousMode { get; set; }

	public object[] Alias { get; set; }

	public object[] CounterDiscontinuityTime { get; set; }

	public object[] HcBitRateIn { get; set; }

	public object[] HcBitRateOut { get; set; }

	public object[] BandwidthUtilization { get; set; }

	public object[] RateData { get; set; }

	public object[] MulticastRateIn { get; set; }

	public object[] MulticastRateOut { get; set; }

	public object[] BroadcastRateIn { get; set; }

	public object[] BroadcastRateOut { get; set; }

	public object[] HcUnicastRateIn { get; set; }

	public object[] HcUnicastRateOut { get; set; }

	public object[] HcMulticastRateIn { get; set; }

	public object[] HcMulticastRateOut { get; set; }

	public object[] HcBroadcastRateIn { get; set; }

	public object[] HcBroadcastRateOut { get; set; }

	public object[] RxBandwidthUtilization { get; set; }

	public object[] TxBandwidthUtilization { get; set; }
}