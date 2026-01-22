namespace QAction_1991
{
	using Skyline.DataMiner.Scripting;
	using Skyline.DataMiner.Utils.Protocol.Extension;

	public class IfTableGetter
	{
		public IfTableGetter(SLProtocol protocol)
		{
			var columnsToGet = new uint[]
			{
				Parameter.Iftable.Idx.iftable_ifindex_1001,
				Parameter.Iftable.Idx.iftable_ifdescr_1002,
				Parameter.Iftable.Idx.iftable_iftype_1003,
				Parameter.Iftable.Idx.iftable_ifmtu_1004,
				Parameter.Iftable.Idx.iftable_ifspeed_1005,
				Parameter.Iftable.Idx.iftable_ifphysaddress_1006,
				Parameter.Iftable.Idx.iftable_ifadminstatus_1007,
				Parameter.Iftable.Idx.iftable_ifoperstatus_1008,
				Parameter.Iftable.Idx.iftable_iflastchange_1009,
				Parameter.Iftable.Idx.iftable_ifoctetsin_1010,
				Parameter.Iftable.Idx.iftable_ifucastpktsin_1012,
				Parameter.Iftable.Idx.iftable_ifdiscardsin_1014,
				Parameter.Iftable.Idx.iftable_iferrorsin_1016,
				Parameter.Iftable.Idx.iftable_ifunknownprotosin_1018,

				Parameter.Iftable.Idx.iftable_ifoctetsout_1011,
				Parameter.Iftable.Idx.iftable_ifucastpktsout_1013,
				Parameter.Iftable.Idx.iftable_ifdiscardsout_1015,
				Parameter.Iftable.Idx.iftable_iferrorsout_1017,

				Parameter.Iftable.Idx.iftable_ratesdata_1019,
				Parameter.Iftable.Idx.iftable_bitratein_1020,
				Parameter.Iftable.Idx.iftable_bitrateout_1021,
				Parameter.Iftable.Idx.iftable_unicastratein_1022,
				Parameter.Iftable.Idx.iftable_unicastrateout_1023,
				Parameter.Iftable.Idx.iftable_discardratein_1024,
				Parameter.Iftable.Idx.iftable_discardrateout_1025,
				Parameter.Iftable.Idx.iftable_errorratein_1026,
				Parameter.Iftable.Idx.iftable_errorrateout_1027,
				Parameter.Iftable.Idx.iftable_unknownprotocolratein_1028,
				Parameter.Iftable.Idx.iftable_rxbandwidthutilization_1029,
				Parameter.Iftable.Idx.iftable_txbandwidthutilization_1030,
			};

			var columns = protocol.GetColumns(Parameter.Iftable.tablePid, columnsToGet);
			int columnPos = -1;

			Keys = (object[])columns[++columnPos];
			Descriptions = (object[])columns[++columnPos];
			Types = (object[])columns[++columnPos];
			Mtu = (object[])columns[++columnPos];
			Speeds = (object[])columns[++columnPos];
			PhysAddress = (object[])columns[++columnPos];
			AdminStatus = (object[])columns[++columnPos];
			OperStatus = (object[])columns[++columnPos];
			LastChange = (object[])columns[++columnPos];
			InOctets = (object[])columns[++columnPos];
			InUnicastPackets = (object[])columns[++columnPos];
			InDiscards = (object[])columns[++columnPos];
			InErrors = (object[])columns[++columnPos];
			InUnknownProtocols = (object[])columns[++columnPos];
			OutOctets = (object[])columns[++columnPos];
			OutUnicastPackets = (object[])columns[++columnPos];
			OutDiscards = (object[])columns[++columnPos];
			OutErrors = (object[])columns[++columnPos];

			RatesData = (object[])columns[++columnPos];
			BitRateIn = (object[])columns[++columnPos];
			BitRateOut = (object[])columns[++columnPos];
			UnicastRateIn = (object[])columns[++columnPos];
			UnicastRateOut = (object[])columns[++columnPos];
			DiscardRateIn = (object[])columns[++columnPos];
			DiscardRateOut = (object[])columns[++columnPos];
			ErrorRateIn = (object[])columns[++columnPos];
			ErrorRateOut = (object[])columns[++columnPos];
			UnknownProtocolRateIn = (object[])columns[++columnPos];
			RxBandwidthUtilization = (object[])columns[++columnPos];
			TxBandwidthUtilization = (object[])columns[++columnPos];
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

		public object[] RatesData { get; set; }

		public object[] BitRateIn { get; set; }

		public object[] BitRateOut { get; set; }

		public object[] UnicastRateIn { get; set; }

		public object[] UnicastRateOut { get; set; }

		public object[] DiscardRateIn { get; set; }

		public object[] DiscardRateOut { get; set; }

		public object[] ErrorRateIn { get; set; }

		public object[] ErrorRateOut { get; set; }

		public object[] UnknownProtocolRateIn { get; set; }

		public object[] RxBandwidthUtilization { get; set; }

		public object[] TxBandwidthUtilization { get; set; }
	}
}