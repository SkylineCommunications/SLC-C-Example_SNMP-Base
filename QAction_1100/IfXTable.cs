namespace Skyline.Protocol.IfxTable
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

	public class IfXTableTimeoutProcessor
	{
		private const int GroupId = 1100;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);

		private readonly IfXTableGetter ifXTableGetter;
		private readonly IfXTableSetter ifXTableSetter;

		private readonly SLProtocol protocol;

		public IfXTableTimeoutProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			ifXTableGetter = new IfXTableGetter(protocol);
			ifXTableGetter.Load();

			ifXTableSetter = new IfXTableSetter(protocol);
		}

		public void ProcessTimeout()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratescalculationsmethod);

			for (int i = 0; i < ifXTableGetter.Keys.Length; i++)
			{
				string key = Convert.ToString(ifXTableGetter.Keys[i]);
				string ratesDataSerialized = Convert.ToString(ifXTableGetter.RatesData[i]);

				var ratesData = IfXTableRatesData.FromJsonString(ratesDataSerialized, MinDelta, MaxDelta);

				ratesData.MulticastRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.MulticastRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.BroadcastRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.BroadcastRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.HcBitRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.HcBitRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.HcUnicastRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.HcUnicastRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.HcMulticastRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.HcMulticastRateOut.BufferDelta(snmpDeltaHelper, key);

				ratesData.HcBroadcastRateIn.BufferDelta(snmpDeltaHelper, key);
				ratesData.HcBroadcastRateOut.BufferDelta(snmpDeltaHelper, key);

				ifXTableSetter.SetColumnsData[Parameter.Ifxtable.tablePid].Add(key);
				ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_ratesdata].Add(ratesData.ToJsonString());
			}
		}

		public void UpdateProtocol()
		{
			ifXTableSetter.SetColumns();
		}

		private class IfXTableGetter
		{
			private readonly SLProtocol protocol;

			public IfXTableGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] RatesData { get; private set; }

			public void Load()
			{
				var columnsToGet = new uint[]
				{
					Parameter.Ifxtable.Idx.ifxtable_ifindex,
					Parameter.Ifxtable.Idx.ifxtable_ratesdata,
				};

				var tableData = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);

				Keys = (object[])tableData[0];
				RatesData = (object[])tableData[1];
			}
		}

		private class IfXTableSetter
		{
			private readonly SLProtocol protocol;

			public IfXTableSetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public Dictionary<int, List<object>> SetColumnsData { get; } = new Dictionary<int, List<object>>
			{
				{ Parameter.Ifxtable.tablePid, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_ratesdata, new List<object>() },
			};

			public void SetColumns()
			{
				protocol.SetColumns(SetColumnsData);
			}
		}
	}

	public class IfXTableProcessor
	{
		private const int GroupId = 1100;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);
		private readonly DuplexGetter duplexGetter;

		private readonly IfXTableGetter ifXTableGetter;
		private readonly IfXTableSetter ifXTableSetter;

		private readonly SLProtocol protocol;

		public IfXTableProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			ifXTableGetter = new IfXTableGetter(protocol);
			ifXTableGetter.Load();

			duplexGetter = new DuplexGetter(protocol);
			duplexGetter.Load();

			ifXTableSetter = new IfXTableSetter(protocol);
		}

		public void ProcessData()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratescalculationsmethod);

			var duplexStatuses = duplexGetter.DuplexStatusesByKey;

			for (int i = 0; i < ifXTableGetter.Keys.Length; i++)
			{
				// Key
				string key = Convert.ToString(ifXTableGetter.Keys[i]);
				ifXTableSetter.SetColumnsData[Parameter.Ifxtable.tablePid].Add(key);

				// Rates
				ProcessRates(snmpDeltaHelper, i, out double bitrateIn, out double bitrateOut);

				// Utilization
				ProcessUtilization(duplexStatuses, i, key, bitrateIn, bitrateOut);
			}

			if (ifXTableGetter.IsSnmpAgentRestarted)
			{
				ifXTableSetter.SetParamsData[Parameter.ifxtablesnmpagentrestartflag] = 0;
			}
		}

		public void UpdateProtocol()
		{
			ifXTableSetter.SetColumns();
			ifXTableSetter.SetParams();
		}

		private static double CalculateRate(string key, ulong count, SnmpDeltaHelper snmpDeltaHelper, SnmpRate64 snmpRateHelper)
		{
			double rate = snmpRateHelper.Calculate(snmpDeltaHelper, count, key);

			return rate;
		}

		private static double CalculateBitRate(string key, ulong octectCount, SnmpDeltaHelper snmpDeltaHelper, SnmpRate64 snmpRateHelper)
		{
			double octetRate = CalculateRate(key, octectCount, snmpDeltaHelper, snmpRateHelper);
			double bitRate = octetRate > 0 ? octetRate * 8 : octetRate;

			return bitRate;
		}

		private void ProcessRates(SnmpDeltaHelper snmpDeltaHelper, int getPosition, out double bitrateIn, out double bitrateOut)
		{
			string key = Convert.ToString(ifXTableGetter.Keys[getPosition]);

			string ratesDataSerialized = Convert.ToString(ifXTableGetter.RateData[getPosition]);
			var ratesData = IfXTableRatesData.FromJsonString(ratesDataSerialized, MinDelta, MaxDelta);

			string discontinuityTime = Convert.ToString(ifXTableGetter.Discontinuity[getPosition]);
			bool hasDiscontinuity = Interface.HasDiscontinuity(discontinuityTime, ratesData.DiscontinuityTime);

			if (ifXTableGetter.IsSnmpAgentRestarted || hasDiscontinuity)
			{
				ratesData.MulticastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.MulticastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.BroadcastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.BroadcastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.HcBitRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.HcBitRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.HcUnicastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.HcUnicastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.HcMulticastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.HcMulticastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.HcBroadcastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				ratesData.HcBroadcastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
			}

			ulong multicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.MulticastPktsIn[getPosition]));
			double multicastRateIn = CalculateRate(key, multicastPktsIn, snmpDeltaHelper, ratesData.MulticastRateIn);

			ulong multicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.MulticastPktsOut[getPosition]));
			double multicastRateOut = CalculateRate(key, multicastPktsOut, snmpDeltaHelper, ratesData.MulticastRateOut);

			ulong broadcastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.BroadcastPktsIn[getPosition]));
			double broadcastRateIn = CalculateRate(key, broadcastPktsIn, snmpDeltaHelper, ratesData.BroadcastRateIn);

			ulong broadcastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.BroadcastPktsOut[getPosition]));
			double broadcastRateOut = CalculateRate(key, broadcastPktsOut, snmpDeltaHelper, ratesData.BroadcastRateOut);

			ulong octetsIn = SafeConvert.ToUInt64(Convert.ToDouble(ifXTableGetter.HCOctetsIn[getPosition]));
			bitrateIn = CalculateBitRate(key, octetsIn, snmpDeltaHelper, ratesData.HcBitRateIn);

			ulong octetsOut = SafeConvert.ToUInt64(Convert.ToDouble(ifXTableGetter.HCOctetsOut[getPosition]));
			bitrateOut = CalculateBitRate(key, octetsOut, snmpDeltaHelper, ratesData.HcBitRateOut);

			ulong hcUnicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.HCUcastPktsIn[getPosition]));
			double hcUnicastRateIn = CalculateRate(key, hcUnicastPktsIn, snmpDeltaHelper, ratesData.HcUnicastRateIn);

			ulong hcUnicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.HCUcastPktsOut[getPosition]));
			double hcUnicastRateOut = CalculateRate(key, hcUnicastPktsOut, snmpDeltaHelper, ratesData.HcUnicastRateOut);

			ulong hcMulticastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.HCMulticastPktsIn[getPosition]));
			double hcMulticastRateIn = CalculateRate(key, hcMulticastPktsIn, snmpDeltaHelper, ratesData.HcMulticastRateIn);

			ulong hcMulticastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.HCMulticastPktsOut[getPosition]));
			double hcMulticastRateOut = CalculateRate(key, hcMulticastPktsOut, snmpDeltaHelper, ratesData.HcMulticastRateOut);

			ulong hcBroadcastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.HCBroadcastPktsIn[getPosition]));
			double hcBroadcastRateIn = CalculateRate(key, hcBroadcastPktsIn, snmpDeltaHelper, ratesData.HcBroadcastRateIn);

			ulong hcBroadcastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.HCBroadcastPktsOut[getPosition]));
			double hcBroadcastRateOut = CalculateRate(key, hcBroadcastPktsOut, snmpDeltaHelper, ratesData.HcBroadcastRateOut);

			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_multicastratein].Add(multicastRateIn);
			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_multicastrateout].Add(multicastRateOut);

			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_broadcastratein].Add(broadcastRateIn);
			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_broadcastrateout].Add(broadcastRateOut);

			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_bitratein].Add(bitrateIn);
			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_bitrateout].Add(bitrateOut);

			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcucastratein].Add(hcUnicastRateIn);
			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcucastrateout].Add(hcUnicastRateOut);

			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcmulticastratein].Add(hcMulticastRateIn);
			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcmulticastrateout].Add(hcMulticastRateOut);

			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcbroadcastratein].Add(hcBroadcastRateIn);
			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcbroadcastrateout].Add(hcBroadcastRateOut);

			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_ratesdata].Add(ratesData.ToJsonString());
		}

		private void ProcessUtilization(Dictionary<string, DuplexStatus> duplexStatuses, int getPosition, string key, double bitrateIn, double bitrateOut)
		{
			double speedValue = GetSpeedValue(getPosition);

			var duplexStatus = duplexStatuses.ContainsKey(key)
				? duplexStatuses[key]
				: DuplexStatus.NotInitialized;

			double utilization = Interface.CalculateUtilization(bitrateIn, bitrateOut, speedValue, duplexStatus);

			ifXTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_bandwidthutilization].Add(utilization);
		}

		private double GetSpeedValue(int getPosition)
		{
			uint speedTableValue = SafeConvert.ToUInt32(Convert.ToDouble(ifXTableGetter.Speed[getPosition]));

			double speedValueToUse = Convert.ToDouble(speedTableValue) * Math.Pow(10, 6);
			return speedValueToUse;
		}

		private class IfXTableGetter
		{
			private readonly SLProtocol protocol;

			public IfXTableGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] MulticastPktsIn { get; private set; }

			public object[] MulticastPktsOut { get; private set; }

			public object[] BroadcastPktsIn { get; private set; }

			public object[] BroadcastPktsOut { get; private set; }

			public object[] HCOctetsIn { get; private set; }

			public object[] HCOctetsOut { get; private set; }

			public object[] HCUcastPktsIn { get; private set; }

			public object[] HCUcastPktsOut { get; private set; }

			public object[] HCMulticastPktsIn { get; private set; }

			public object[] HCMulticastPktsOut { get; private set; }

			public object[] HCBroadcastPktsIn { get; private set; }

			public object[] HCBroadcastPktsOut { get; private set; }

			public object[] Speed { get; private set; }

			public object[] Discontinuity { get; private set; }

			public object[] RateData { get; private set; }

			public bool IsSnmpAgentRestarted { get; private set; }

			public void Load()
			{
				IsSnmpAgentRestarted = Convert.ToBoolean(protocol.GetParameter(Parameter.ifxtablesnmpagentrestartflag));

				var ifXTableColumnsToGetIDXs = new uint[]
				{
					Parameter.Ifxtable.Idx.ifxtable_ifindex,
					Parameter.Ifxtable.Idx.ifxtable_ifmulticastpktsin,
					Parameter.Ifxtable.Idx.ifxtable_ifmulticastpktsout,
					Parameter.Ifxtable.Idx.ifxtable_ifbroadcastpktsin,
					Parameter.Ifxtable.Idx.ifxtable_ifbroadcastpktsout,
					Parameter.Ifxtable.Idx.ifxtable_ifhcoctetsin,
					Parameter.Ifxtable.Idx.ifxtable_ifhcoctetsout,
					Parameter.Ifxtable.Idx.ifxtable_ifhcucastpktsin,
					Parameter.Ifxtable.Idx.ifxtable_ifhcucastpktsout,
					Parameter.Ifxtable.Idx.ifxtable_ifhcmulticastpktsin,
					Parameter.Ifxtable.Idx.ifxtable_ifhcmulticastpktsout,
					Parameter.Ifxtable.Idx.ifxtable_ifhcbroadcastpktsin,
					Parameter.Ifxtable.Idx.ifxtable_ifhcbroadcastpktsout,
					Parameter.Ifxtable.Idx.ifxtable_ifhighspeed,
					Parameter.Ifxtable.Idx.ifxtable_ifcounterdiscontinuitytime,
					Parameter.Ifxtable.Idx.ifxtable_ratesdata,
				};

				var ifXTableColumns = protocol.GetColumns(Parameter.Ifxtable.tablePid, ifXTableColumnsToGetIDXs);

				Keys = (object[])ifXTableColumns[0];
				MulticastPktsIn = (object[])ifXTableColumns[1];
				MulticastPktsOut = (object[])ifXTableColumns[2];
				BroadcastPktsIn = (object[])ifXTableColumns[3];
				BroadcastPktsOut = (object[])ifXTableColumns[4];
				HCOctetsIn = (object[])ifXTableColumns[5];
				HCOctetsOut = (object[])ifXTableColumns[6];
				HCUcastPktsIn = (object[])ifXTableColumns[7];
				HCUcastPktsOut = (object[])ifXTableColumns[8];
				HCMulticastPktsIn = (object[])ifXTableColumns[9];
				HCMulticastPktsOut = (object[])ifXTableColumns[10];
				HCBroadcastPktsIn = (object[])ifXTableColumns[11];
				HCBroadcastPktsOut = (object[])ifXTableColumns[12];
				Speed = (object[])ifXTableColumns[13];
				Discontinuity = (object[])ifXTableColumns[14];
				RateData = (object[])ifXTableColumns[15];
			}
		}

		private class IfXTableSetter
		{
			private readonly SLProtocol protocol;

			public IfXTableSetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public Dictionary<int, List<object>> SetColumnsData { get; } = new Dictionary<int, List<object>>
			{
				{ Parameter.Ifxtable.tablePid, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_multicastratein, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_multicastrateout, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_broadcastratein, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_broadcastrateout, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_bitratein, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_bitrateout, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcucastratein, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcucastrateout, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcmulticastratein, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcmulticastrateout, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcbroadcastratein, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcbroadcastrateout, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_bandwidthutilization, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_ratesdata, new List<object>() },
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