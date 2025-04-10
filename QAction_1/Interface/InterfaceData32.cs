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

		public SnmpRate32 Discardratein { get; set; }

		public SnmpRate32 Discardrateout { get; set; }

		public SnmpRate32 Errorratein { get; set; }

		public SnmpRate32 Errorrateout { get; set; }

		public SnmpRate32 Unknownprotosin { get; set; }

		public string DiscontinuityTime { get; set; }

		public static InterfaceData32 FromJsonString(string serializedIfRateData, TimeSpan minDelta, TimeSpan maxDelta, RateBase rateBase = RateBase.Second)
		{
			if (String.IsNullOrWhiteSpace(serializedIfRateData))
			{
				return new InterfaceData32
				{
					BitrateIn = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					BitrateOut = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					UnicastrateIn = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					UnicastrateOut = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					Discardratein = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					Discardrateout = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					Errorratein = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					Errorrateout = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					Unknownprotosin = SnmpRate32.FromJsonString(String.Empty, minDelta, maxDelta, rateBase),
					DiscontinuityTime = String.Empty,
				};
			}

			return JsonConvert.DeserializeObject<InterfaceData32>(serializedIfRateData);
		}

		public string ToJsonString()
		{
			return JsonConvert.SerializeObject(this);
		}
	}
}