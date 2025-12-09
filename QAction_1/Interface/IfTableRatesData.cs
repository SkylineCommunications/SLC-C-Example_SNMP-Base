namespace Skyline.Protocol.Interface
{
	using System;
	using Newtonsoft.Json;
	using Skyline.DataMiner.Utils.Rates.Common;
	using Skyline.DataMiner.Utils.Rates.Protocol;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;

	public class IfTableRatesData
	{
		public SnmpRate32 BitRateIn { get; set; }

		public SnmpRate32 BitRateOut { get; set; }

		public SnmpRate32 UnicastRateIn { get; set; }

		public SnmpRate32 UnicastRateOut { get; set; }

		public SnmpRate32 DiscardRateIn { get; set; }

		public SnmpRate32 DiscardRateOut { get; set; }

		public SnmpRate32 ErrorRateIn { get; set; }

		public SnmpRate32 ErrorRateOut { get; set; }

		public SnmpRate32 UnknownProtocolsRateIn { get; set; }

		public string DiscontinuityTime { get; private set; }

		public static IfTableRatesData FromJsonString(string ratesDataSerialized, TimeSpan minDelta, TimeSpan maxDelta, RateBase rateBase = RateBase.Second)
		{
			if (string.IsNullOrWhiteSpace(ratesDataSerialized))
			{
				return new IfTableRatesData
				{
					BitRateIn = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					BitRateOut = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					UnicastRateIn = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					UnicastRateOut = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					DiscardRateIn = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					DiscardRateOut = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					ErrorRateIn = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					ErrorRateOut = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					UnknownProtocolsRateIn = SnmpRate32.FromJsonString(string.Empty, minDelta, maxDelta, rateBase),
					DiscontinuityTime = string.Empty,
				};
			}

			return SecureNewtonsoftDeserialization.DeserializeObject<IfTableRatesData>(ratesDataSerialized);
		}

		public string ToJsonString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}