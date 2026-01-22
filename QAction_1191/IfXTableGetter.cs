namespace QAction_1991
{
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

				Parameter.Ifxtable.Idx.ifxtable_ratesdata_1121,
				Parameter.Ifxtable.Idx.ifxtable_bitratein_1122,
				Parameter.Ifxtable.Idx.ifxtable_bitrateout_1123,
				Parameter.Ifxtable.Idx.ifxtable_multicastratein_1124,
				Parameter.Ifxtable.Idx.ifxtable_multicastrateout_1125,
				Parameter.Ifxtable.Idx.ifxtable_broadcastratein_1126,
				Parameter.Ifxtable.Idx.ifxtable_broadcastrateout_1127,
				Parameter.Ifxtable.Idx.ifxtable_hcucastratein_1128,
				Parameter.Ifxtable.Idx.ifxtable_hcucastrateout_1129,
				Parameter.Ifxtable.Idx.ifxtable_hcmulticastratein_1130,
				Parameter.Ifxtable.Idx.ifxtable_hcmulticastrateout_1131,
				Parameter.Ifxtable.Idx.ifxtable_hcbroadcastratein_1132,
				Parameter.Ifxtable.Idx.ifxtable_hcbroadcastrateout_1133,

				Parameter.Ifxtable.Idx.ifxtable_rxbandwidthutilization_1134,
				Parameter.Ifxtable.Idx.ifxtable_txbandwidthutilization_1135,
			};

			var columns = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);
			int columnPos = -1;

			Keys = (object[])columns[++columnPos];
			Name = (object[])columns[++columnPos];
			InMulticastPackets = (object[])columns[++columnPos];
			InBroadcastPackets = (object[])columns[++columnPos];
			OutMulticastPackets = (object[])columns[++columnPos];
			OutBroadcastPackets = (object[])columns[++columnPos];
			HcInOctets = (object[])columns[++columnPos];
			HcInUnicastPackets = (object[])columns[++columnPos];
			HcInMulticastPackets = (object[])columns[++columnPos];
			HcInBroadcastPackets = (object[])columns[++columnPos];
			HcOutOctets = (object[])columns[++columnPos];
			HcOutUnicastPackets = (object[])columns[++columnPos];
			HcOutMulticastPackets = (object[])columns[++columnPos];
			HcOutBroadcastPackets = (object[])columns[++columnPos];
			HighSpeed = (object[])columns[++columnPos];
			PromiscuousMode = (object[])columns[++columnPos];
			Alias = (object[])columns[++columnPos];
			CounterDiscontinuityTime = (object[])columns[++columnPos];

			RatesData = (object[])columns[++columnPos];
			HcBitRateIn = (object[])columns[++columnPos];
			HcBitRateOut = (object[])columns[++columnPos];
			MulticastRateIn = (object[])columns[++columnPos];
			MulticastRateOut = (object[])columns[++columnPos];
			BroadcastRateIn = (object[])columns[++columnPos];
			BroadcastRateOut = (object[])columns[++columnPos];
			HcUnicastRateIn = (object[])columns[++columnPos];
			HcUnicastRateOut = (object[])columns[++columnPos];
			HcMulticastRateIn = (object[])columns[++columnPos];
			HcMulticastRateOut = (object[])columns[++columnPos];
			HcBroadcastRateIn = (object[])columns[++columnPos];
			HcBroadcastRateOut = (object[])columns[++columnPos];

			RxBandwidthUtilization = (object[])columns[++columnPos];
			TxBandwidthUtilization = (object[])columns[++columnPos];
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

		public object[] RatesData { get; set; }

		public object[] HcBitRateIn { get; set; }

		public object[] HcBitRateOut { get; set; }

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
}