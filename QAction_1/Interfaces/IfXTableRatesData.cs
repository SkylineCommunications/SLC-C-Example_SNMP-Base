namespace Skyline.Protocol.Interfaces
{
	using System;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Utils.Rates.Common;
	using Skyline.DataMiner.Utils.Rates.Protocol;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;

	public class IfXTableRatesData
	{
		public SnmpRate64 MulticastRateIn { get; set; }

		public SnmpRate64 MulticastRateOut { get; set; }

		public SnmpRate64 BroadcastRateIn { get; set; }

		public SnmpRate64 BroadcastRateOut { get; set; }

		public SnmpRate64 HcBitRateIn { get; set; }

		public SnmpRate64 HcBitRateOut { get; set; }

		public SnmpRate64 HcUnicastRateIn { get; set; }

		public SnmpRate64 HcUnicastRateOut { get; set; }

		public SnmpRate64 HcMulticastRateIn { get; set; }

		public SnmpRate64 HcMulticastRateOut { get; set; }

		public SnmpRate64 HcBroadcastRateIn { get; set; }

		public SnmpRate64 HcBroadcastRateOut { get; set; }

		public string DiscontinuityTime { get; private set; }

		public static IfXTableRatesData FromJsonString(string ratesDataSerialized, TimeSpan minDelta, TimeSpan maxDelta, RateBase rateBase = RateBase.Second)
		{
			if (string.IsNullOrWhiteSpace(ratesDataSerialized))
			{
				return new IfXTableRatesData
				{
					MulticastRateIn = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					MulticastRateOut = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					BroadcastRateIn = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					BroadcastRateOut = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					HcBitRateIn = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					HcBitRateOut = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					HcUnicastRateIn = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					HcUnicastRateOut = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					HcMulticastRateIn = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					HcMulticastRateOut = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					HcBroadcastRateIn = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					HcBroadcastRateOut = SnmpRate64.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					DiscontinuityTime = string.Empty,
				};
			}

			return SecureNewtonsoftDeserialization.DeserializeObject<IfXTableRatesData>(ratesDataSerialized);
		}

		public string ToJsonString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}