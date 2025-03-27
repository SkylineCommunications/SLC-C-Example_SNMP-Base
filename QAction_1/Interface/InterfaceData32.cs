namespace Skyline.Protocol.Interface
{
	using System;

	using Newtonsoft.Json;

	using Skyline.DataMiner.Utils.Rates.Common;
	using Skyline.DataMiner.Utils.Rates.Protocol;

	public class InterfaceData32
	{
		public SnmpRate32 BitrateIn { get; set; }

		public SnmpRate32 BitrateOut { get; set; }

		public SnmpRate32 UnicastrateIn { get; set; }

		public SnmpRate32 UnicastrateOut { get; set; }

		public string DiscontinuityTime { get; set; }

		public static InterfaceData32 FromJsonString(string serializedIfxRateData, TimeSpan minDelta, TimeSpan maxDelta, RateBase rateBase = RateBase.Second)
		{
			if (String.IsNullOrWhiteSpace(serializedIfxRateData))
			{
				return new InterfaceData32
				{
					BitrateIn = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					BitrateOut = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					UnicastrateIn = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					UnicastrateOut = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					DiscontinuityTime = String.Empty,
				};
			}

			return JsonConvert.DeserializeObject<InterfaceData32>(serializedIfxRateData);
		}

		public string ToJsonString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}