namespace QAction_1000.IfTable
{
    using System;
    using System.Collections.Generic;

    using Skyline.DataMiner.Scripting;
    using Skyline.DataMiner.Utils.Interfaces;
    using Skyline.DataMiner.Utils.Protocol.Extension;
    using Skyline.DataMiner.Utils.Rates.Protocol;
    using Skyline.DataMiner.Utils.SafeConverters;
    using Skyline.DataMiner.Utils.SNMP;
    using Skyline.Protocol.Interfaces;

    public class IfTableTimeoutProcessor
	{
		private const int GroupId = 1000;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);

		private readonly IfTableGetter ifTableGetter;
		private readonly IfTableSetter ifTableSetter;

		private readonly SLProtocol protocol;

		public IfTableTimeoutProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			ifTableGetter = new IfTableGetter(protocol);
			ifTableGetter.Load();

			ifTableSetter = new IfTableSetter(protocol);
		}

		public void ProcessTimeout()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratescalculationsmethod);

			for (int i = 0; i < ifTableGetter.Keys.Length; i++)
			{
				string key = Convert.ToString(ifTableGetter.Keys[i]);
				string ratesDataSerialized = Convert.ToString(ifTableGetter.RatesData[i]);

				var ratesData = IfTableRatesData.FromJsonString(ratesDataSerialized, MinDelta, MaxDelta);

				ratesData.BitRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.BitRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.UnicastRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.UnicastRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.DiscardRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.DiscardRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.ErrorRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.ErrorRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.UnknownProtocolsRateIn.BufferDelta(snmpDeltaHelper, key);

				ifTableSetter.SetColumnsData[Parameter.Iftable.tablePid].Add(key);
				ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_ratesdata].Add(ratesData.ToJsonString());
			}
		}

		public void UpdateProtocol()
		{
			ifTableSetter.SetColumns();
		}

		private sealed class IfTableGetter
		{
			private readonly SLProtocol protocol;

			public IfTableGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] RatesData { get; private set; }

			public void Load()
			{
				var columnsToGet = new uint[]
				{
					Parameter.Iftable.Idx.iftable_ifindex,
					Parameter.Iftable.Idx.iftable_ratesdata,
				};

				var tableData = protocol.GetColumns(Parameter.Iftable.tablePid, columnsToGet);

				Keys = (object[])tableData[0];
				RatesData = (object[])tableData[1];
			}
		}

		private sealed class IfTableSetter
		{
			private readonly SLProtocol protocol;

			public IfTableSetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public Dictionary<int, List<object>> SetColumnsData { get; } = new Dictionary<int, List<object>>
			{
				{ Parameter.Iftable.tablePid, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_ratesdata, new List<object>() },
			};

			public void SetColumns()
			{
				protocol.SetColumns(SetColumnsData);
			}
		}
	}

	public class IfTableProcessor
	{
		private const int GroupId = 1000;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);

		private readonly IfTableGetter ifTableGetter;
		private readonly IfTableSetter ifTableSetter;

		private readonly SLProtocol protocol;

		public IfTableProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			ifTableGetter = new IfTableGetter(protocol);
			ifTableGetter.Load();

			ifTableSetter = new IfTableSetter(protocol);
		}

		public void ProcessData()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratescalculationsmethod);

			for (int i = 0; i < ifTableGetter.Keys.Length; i++)
			{
				// Key
				string key = Convert.ToString(ifTableGetter.Keys[i]);
				ifTableSetter.SetColumnsData[Parameter.Iftable.tablePid].Add(key);

				// Rates
				ProcessRates(snmpDeltaHelper, i, out double bitrateIn, out double bitrateOut);

				// Utilization
				ProcessUtilization(i, bitrateIn, bitrateOut);
			}

			if (ifTableGetter.IsSnmpAgentRestarted)
			{
				ifTableSetter.SetParamsData[Parameter.iftablesnmpagentrestartflag] = 0;
			}
		}

		public void UpdateProtocol()
		{
			ifTableSetter.SetColumns();
			ifTableSetter.SetParams();
		}

		private static double CalculateRate(string key, uint count, SnmpDeltaHelper snmpDeltaHelper, SnmpRate32 snmpRateHelper)
		{
			double rate = snmpRateHelper.Calculate(snmpDeltaHelper, count, key);

			return rate;
		}

		private static double CalculateBitRate(string key, uint octetCount, SnmpDeltaHelper snmpDeltaHelper, SnmpRate32 snmpRateHelper)
		{
			double octetRate = CalculateRate(key, octetCount, snmpDeltaHelper, snmpRateHelper);
			double bitRate = octetRate > 0 ? octetRate * 8 : octetRate;

			return bitRate;
		}

		private void ProcessRates(SnmpDeltaHelper snmpDeltaHelper, int getPosition, out double bitRateIn, out double bitRateOut)
		{
			string key = Convert.ToString(ifTableGetter.Keys[getPosition]);

			string ratesDataSerialized = Convert.ToString(ifTableGetter.RatesData[getPosition]);
			var ratesData = IfTableRatesData.FromJsonString(ratesDataSerialized, MinDelta, MaxDelta);

			string discontinuityTime = Convert.ToString(ifTableGetter.Discontinuity[getPosition]);
			bool hasDiscontinuity = Interface.HasDiscontinuity(discontinuityTime, ratesData.DiscontinuityTime);

			if (ifTableGetter.IsSnmpAgentRestarted || hasDiscontinuity)
			{
				ratesData.BitRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.BitRateOut = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.UnicastRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.UnicastRateOut = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.DiscardRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.DiscardRateOut = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.ErrorRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.ErrorRateOut = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.UnknownProtocolsRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
			}

			uint octetsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.OctetsIn[getPosition]));
			bitRateIn = CalculateBitRate(key, octetsIn, snmpDeltaHelper, ratesData.BitRateIn);

			uint octetsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.OctetsOut[getPosition]));
			bitRateOut = CalculateBitRate(key, octetsOut, snmpDeltaHelper, ratesData.BitRateOut);

			uint unicastIn = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.UnicastPacketsIn[getPosition]));
			double unicastRateIn = CalculateRate(key, unicastIn, snmpDeltaHelper, ratesData.UnicastRateIn);

			uint unicastOut = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.UnicastPacketsOut[getPosition]));
			double unicastRateOut = CalculateRate(key, unicastOut, snmpDeltaHelper, ratesData.UnicastRateOut);

			uint discardsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.DiscardsIn[getPosition]));
			double discardRateIn = CalculateRate(key, discardsIn, snmpDeltaHelper, ratesData.DiscardRateIn);

			uint discardsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.DiscardsOut[getPosition]));
			double discardRateOut = CalculateRate(key, discardsOut, snmpDeltaHelper, ratesData.DiscardRateOut);

			uint errorsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.ErrorsIn[getPosition]));
			double errorRateIn = CalculateRate(key, errorsIn, snmpDeltaHelper, ratesData.ErrorRateIn);

			uint errorsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.ErrorsOut[getPosition]));
			double errorRateOut = CalculateRate(key, errorsOut, snmpDeltaHelper, ratesData.ErrorRateOut);

			uint unknownProtocolsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.UnknownIn[getPosition]));
			double unknownProtocolRateIn = CalculateRate(key, unknownProtocolsIn, snmpDeltaHelper, ratesData.UnknownProtocolsRateIn);

			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_bitratein].Add(bitRateIn);
			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_bitrateout].Add(bitRateOut);

			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_unicastratein].Add(unicastRateIn);
			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_unicastrateout].Add(unicastRateOut);

			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_discardratein].Add(discardRateIn);
			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_discardrateout].Add(discardRateOut);

			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_errorratein].Add(errorRateIn);
			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_errorrateout].Add(errorRateOut);

			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_unknownprotocolratein].Add(unknownProtocolRateIn);

			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_ratesdata].Add(ratesData.ToJsonString());
		}

		private void ProcessUtilization(int getPosition, double bitrateIn, double bitrateOut)
		{
			double speedValue = GetSpeedValue(getPosition);

			double rxUtilitzation = BandwidthHelper.CalculateUtilization(bitrateIn, speedValue);
			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_rxbandwidthutilization].Add(rxUtilitzation);

			double txUtilitzation = BandwidthHelper.CalculateUtilization(bitrateOut, speedValue);
			ifTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_txbandwidthutilization].Add(txUtilitzation);
		}

		private double GetSpeedValue(int getPosition)
		{
			uint speedTableValue = SafeConvert.ToUInt32(Convert.ToDouble(ifTableGetter.Speed[getPosition]));

			return speedTableValue == uint.MaxValue
				? -1.0
				: Convert.ToDouble(speedTableValue);
		}

		private sealed class IfTableGetter
		{
			private readonly SLProtocol protocol;

			public IfTableGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] OctetsIn { get; private set; }

			public object[] OctetsOut { get; private set; }

			public object[] UnicastPacketsIn { get; private set; }

			public object[] UnicastPacketsOut { get; private set; }

			public object[] DiscardsIn { get; private set; }

			public object[] DiscardsOut { get; private set; }

			public object[] ErrorsIn { get; private set; }

			public object[] ErrorsOut { get; private set; }

			public object[] UnknownIn { get; private set; }

			public object[] Speed { get; private set; }

			public object[] Discontinuity { get; private set; }

			public object[] RatesData { get; private set; }

			public bool IsSnmpAgentRestarted { get; private set; }

			public void Load()
			{
				IsSnmpAgentRestarted = Convert.ToBoolean(protocol.GetParameter(Parameter.iftablesnmpagentrestartflag));
				LoadIfTable();
				LoadIfXTable();
			}

			private void LoadIfTable()
			{
				var ifTableColumnsToGetIDXs = new uint[]
				{
					Parameter.Iftable.Idx.iftable_ifindex,
					Parameter.Iftable.Idx.iftable_ifoctetsin,
					Parameter.Iftable.Idx.iftable_ifoctetsout,
					Parameter.Iftable.Idx.iftable_ifspeed,
					Parameter.Iftable.Idx.iftable_ifucastpktsin,
					Parameter.Iftable.Idx.iftable_ifucastpktsout,
					Parameter.Iftable.Idx.iftable_ifdiscardsin,
					Parameter.Iftable.Idx.iftable_ifdiscardsout,
					Parameter.Iftable.Idx.iftable_iferrorsin,
					Parameter.Iftable.Idx.iftable_iferrorsout,
					Parameter.Iftable.Idx.iftable_ifunknownprotosin,
					Parameter.Iftable.Idx.iftable_ratesdata,
				};

				var ifTableColumns = protocol.GetColumns(Parameter.Iftable.tablePid, ifTableColumnsToGetIDXs);
				Keys = (object[])ifTableColumns[0];
				OctetsIn = (object[])ifTableColumns[1];
				OctetsOut = (object[])ifTableColumns[2];
				Speed = (object[])ifTableColumns[3];
				UnicastPacketsIn = (object[])ifTableColumns[4];
				UnicastPacketsOut = (object[])ifTableColumns[5];
				DiscardsIn = (object[])ifTableColumns[6];
				DiscardsOut = (object[])ifTableColumns[7];
				ErrorsIn = (object[])ifTableColumns[8];
				ErrorsOut = (object[])ifTableColumns[9];
				UnknownIn = (object[])ifTableColumns[10];
				RatesData = (object[])ifTableColumns[11];

				Discontinuity = new object[Keys.Length]; // Will be filled in via LoadIfXTable
			}

			private void LoadIfXTable()
			{
				var columnsToGet = new uint[]
				{
					Parameter.Ifxtable.Idx.ifxtable_ifindex,
					Parameter.Ifxtable.Idx.ifxtable_ifcounterdiscontinuitytime,
				};

				var ifXTableColumns = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);
				var ifXTableKeys = (object[])ifXTableColumns[0];
				var ifXTableDiscontinuities = (object[])ifXTableColumns[1];

				for (int i = 0; i < ifXTableKeys.Length; i++)
				{
					int position = Array.IndexOf(Keys, ifXTableKeys[i]);
					if (position > -1)
					{
						Discontinuity[position] = ifXTableDiscontinuities[i];
					}
				}
			}
		}

		private sealed class IfTableSetter
		{
			private readonly SLProtocol protocol;

			public IfTableSetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public Dictionary<int, List<object>> SetColumnsData { get; } = new Dictionary<int, List<object>>
			{
				{ Parameter.Iftable.tablePid, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_bitratein, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_bitrateout, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_rxbandwidthutilization, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_txbandwidthutilization, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_ratesdata, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_unicastratein, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_unicastrateout, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_discardratein, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_discardrateout, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_errorratein, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_errorrateout, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_unknownprotocolratein, new List<object>() },
			};

			internal Dictionary<int, object> SetParamsData { get; } = new Dictionary<int, object>();

			public void SetColumns()
			{
				protocol.SetColumns(SetColumnsData);
			}

			public void SetParams()
			{
				protocol.SetParameters(SetParamsData);
			}
		}
	}
}