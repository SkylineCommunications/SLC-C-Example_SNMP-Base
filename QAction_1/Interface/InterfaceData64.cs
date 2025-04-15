namespace Skyline.Protocol.Interface
{
	using System;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Utils.Rates.Common;
	using Skyline.DataMiner.Utils.Rates.Protocol;
	using Skyline.DataMiner.Utils.SecureCoding.SecureSerialization.Json.Newtonsoft;

	public class InterfaceData64
	{
		public SnmpRate64 MulticastRateIn { get; set; }

		public SnmpRate64 MulticastRateOut { get; set; }

		public SnmpRate64 BroadcastRateIn { get; set; }

		public SnmpRate64 BroadcastRateOut { get; set; }

		public SnmpRate64 HCBitrateIn { get; set; }

		public SnmpRate64 HCBitrateOut { get; set; }

		public SnmpRate64 HCUcastRateIn { get; set; }

		public SnmpRate64 HCUcastRateOut { get; set; }

		public SnmpRate64 HCMulticastRateIn { get; set; }

		public SnmpRate64 HCMulticastRateOut { get; set; }

		public SnmpRate64 HCBroadcastRateIn { get; set; }

		public SnmpRate64 HCBroadcastRateOut { get; set; }

		public string DiscontinuityTime { get; set; }

		public static InterfaceData64 FromJsonString(string serializedIfxRateData, TimeSpan minDelta, TimeSpan maxDelta, RateBase rateBase = RateBase.Second)
		{
			if (String.IsNullOrWhiteSpace(serializedIfxRateData))
			{
				return new InterfaceData64
				{
					MulticastRateIn = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					MulticastRateOut = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					BroadcastRateIn = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					BroadcastRateOut = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					HCBitrateIn = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					HCBitrateOut = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					HCUcastRateIn = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					HCUcastRateOut = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					HCMulticastRateIn = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					HCMulticastRateOut = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					HCBroadcastRateIn = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					HCBroadcastRateOut = SnmpRate64.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					DiscontinuityTime = String.Empty,
				};
			}

			return SecureNewtonsoftDeserialization.DeserializeObject<InterfaceData64>(serializedIfxRateData);
		}

		public string ToJsonString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}