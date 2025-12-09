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
	using Skyline.Protocol.Interface;

	public class IfxTableTimeoutProcessor
	{
		private const int GroupId = 1100;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);

		private readonly IfxTableGetter ifxTableGetter;
		private readonly IfxTableSetter ifxTableSetter;

		private readonly SLProtocol protocol;

		public IfxTableTimeoutProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			ifxTableGetter = new IfxTableGetter(protocol);
			ifxTableGetter.Load();

			ifxTableSetter = new IfxTableSetter(protocol);
		}

		public void ProcessTimeout()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratescalculationsmethod);

			for (var i = 0; i < ifxTableGetter.Keys.Length; i++)
			{
				var key = Convert.ToString(ifxTableGetter.Keys[i]);
				var ratesDataSerialized = Convert.ToString(ifxTableGetter.RatesData[i]);

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

				ifxTableSetter.SetColumnsData[Parameter.Ifxtable.tablePid].Add(key);
				ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_ratesdata].Add(ratesData.ToJsonString());
			}
		}

		public void UpdateProtocol()
		{
			ifxTableSetter.SetColumns();
		}

		private class IfxTableGetter
		{
			private readonly SLProtocol protocol;

			public IfxTableGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] RatesData { get; private set; }

			public void Load()
			{
				var columnsToGet = new uint[] { Parameter.Ifxtable.Idx.ifxtable_ifindex, Parameter.Ifxtable.Idx.ifxtable_ratesdata };

				var tableData = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);

				Keys = (object[])tableData[0];
				RatesData = (object[])tableData[1];
			}
		}

		private class IfxTableSetter
		{
			private readonly SLProtocol protocol;

			public IfxTableSetter(SLProtocol protocol)
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

	public class IfxTableProcessor
	{
		private const int GroupId = 1100;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);
		private readonly DuplexGetter duplexGetter;

		private readonly IfXTableGetter ifxTableGetter;
		private readonly IfxTableSetter ifxTableSetter;

		private readonly SLProtocol protocol;

		public IfxTableProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			ifxTableGetter = new IfXTableGetter(protocol);
			ifxTableGetter.Load();

			duplexGetter = new DuplexGetter(protocol);
			duplexGetter.Load();

			ifxTableSetter = new IfxTableSetter(protocol);
		}

		public void ProcessData()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratescalculationsmethod);

			var duplexStatuses = ConvertDuplexColumnToDictionary();

			for (var i = 0; i < ifxTableGetter.Keys.Length; i++)
			{
				// Key
				var key = Convert.ToString(ifxTableGetter.Keys[i]);
				ifxTableSetter.SetColumnsData[Parameter.Ifxtable.tablePid].Add(key);

				// Rates
				ProcessRates(snmpDeltaHelper, i, out var bitrateIn, out var bitrateOut);

				// Utilization
				ProcessUtilization(duplexStatuses, i, key, bitrateIn, bitrateOut);
			}

			if (ifxTableGetter.IsSnmpAgentRestarted)
			{
				ifxTableSetter.SetParamsData[Parameter.ifxtablesnmpagentrestartflag] = 0;
			}
		}

		public void UpdateProtocol()
		{
			ifxTableSetter.SetColumns();
			ifxTableSetter.SetParams();
		}

		private static double CalculateRate(string key, ulong count, SnmpDeltaHelper snmpDeltaHelper, SnmpRate64 snmpRateHelper)
		{
			var rate = snmpRateHelper.Calculate(snmpDeltaHelper, count, key);

			return rate;
		}

		private static double CalculateBitRate(string key, ulong octectCount, SnmpDeltaHelper snmpDeltaHelper, SnmpRate64 snmpRateHelper)
		{
			var octetRate = CalculateRate(key, octectCount, snmpDeltaHelper, snmpRateHelper);
			var bitRate = octetRate > 0 ? octetRate * 8 : octetRate;

			return bitRate;
		}

		private void ProcessRates(SnmpDeltaHelper snmpDeltaHelper, int getPosition, out double bitrateIn, out double bitrateOut)
		{
			var key = Convert.ToString(ifxTableGetter.Keys[getPosition]);

			var ratesDataSerialized = Convert.ToString(ifxTableGetter.RateData[getPosition]);
			var ratesData = IfXTableRatesData.FromJsonString(ratesDataSerialized, MinDelta, MaxDelta);

			var discontinuityTime = Convert.ToString(ifxTableGetter.Discontinuity[getPosition]);
			var hasDiscontinuity = Interface.HasDiscontinuity(discontinuityTime, ratesData.DiscontinuityTime);

			if (ifxTableGetter.IsSnmpAgentRestarted || hasDiscontinuity)
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

			ulong multicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.MulticastPktsIn[getPosition]));
			var multicastRateIn = CalculateRate(key, multicastPktsIn, snmpDeltaHelper, ratesData.MulticastRateIn);

			ulong multicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.MulticastPktsOut[getPosition]));
			var multicastRateOut = CalculateRate(key, multicastPktsOut, snmpDeltaHelper, ratesData.MulticastRateOut);

			ulong broadcastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.BroadcastPktsIn[getPosition]));
			var broadcastRateIn = CalculateRate(key, broadcastPktsIn, snmpDeltaHelper, ratesData.BroadcastRateIn);

			ulong broadcastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.BroadcastPktsOut[getPosition]));
			var broadcastRateOut = CalculateRate(key, broadcastPktsOut, snmpDeltaHelper, ratesData.BroadcastRateOut);

			var octetsIn = SafeConvert.ToUInt64(Convert.ToDouble(ifxTableGetter.HCOctetsIn[getPosition]));
			bitrateIn = CalculateBitRate(key, octetsIn, snmpDeltaHelper, ratesData.HcBitRateIn);

			var octetsOut = SafeConvert.ToUInt64(Convert.ToDouble(ifxTableGetter.HCOctetsOut[getPosition]));
			bitrateOut = CalculateBitRate(key, octetsOut, snmpDeltaHelper, ratesData.HcBitRateOut);

			ulong hcUnicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCUcastPktsIn[getPosition]));
			var hcUnicastRateIn = CalculateRate(key, hcUnicastPktsIn, snmpDeltaHelper, ratesData.HcUnicastRateIn);

			ulong hcUnicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCUcastPktsOut[getPosition]));
			var hcUnicastRateOut = CalculateRate(key, hcUnicastPktsOut, snmpDeltaHelper, ratesData.HcUnicastRateOut);

			ulong hcMulticastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCMulticastPktsIn[getPosition]));
			var hcMulticastRateIn = CalculateRate(key, hcMulticastPktsIn, snmpDeltaHelper, ratesData.HcMulticastRateIn);

			ulong hcMulticastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCMulticastPktsOut[getPosition]));
			var hcMulticastRateOut = CalculateRate(key, hcMulticastPktsOut, snmpDeltaHelper, ratesData.HcMulticastRateOut);

			ulong hcBroadcastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCBroadcastPktsIn[getPosition]));
			var hcBroadcastRateIn = CalculateRate(key, hcBroadcastPktsIn, snmpDeltaHelper, ratesData.HcBroadcastRateIn);

			ulong hcBroadcastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCBroadcastPktsOut[getPosition]));
			var hcBroadcastRateOut = CalculateRate(key, hcBroadcastPktsOut, snmpDeltaHelper, ratesData.HcBroadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_multicastratein].Add(multicastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_multicastrateout].Add(multicastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_broadcastratein].Add(broadcastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_broadcastrateout].Add(broadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_bitratein].Add(bitrateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_bitrateout].Add(bitrateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcucastratein].Add(hcUnicastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcucastrateout].Add(hcUnicastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcmulticastratein].Add(hcMulticastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcmulticastrateout].Add(hcMulticastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcbroadcastratein].Add(hcBroadcastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcbroadcastrateout].Add(hcBroadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_ratesdata].Add(ratesData.ToJsonString());
		}

		private void ProcessUtilization(Dictionary<string, DuplexStatus> duplexStatuses, int getPosition, string key, double bitrateIn, double bitrateOut)
		{
			var speedValue = GetSpeedValue(getPosition);

			var duplexStatus = duplexStatuses.ContainsKey(key)
				? duplexStatuses[key]
				: DuplexStatus.NotInitialized;

			var utilization = Interface.CalculateUtilization(bitrateIn, bitrateOut, speedValue, duplexStatus);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_bandwidthutilization].Add(utilization);
		}

		private double GetSpeedValue(int getPosition)
		{
			var speedTableValue = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.Speed[getPosition]));

			var speedValueToUse = Convert.ToDouble(speedTableValue) * Math.Pow(10, 6);
			return speedValueToUse;
		}

		private Dictionary<string, DuplexStatus> ConvertDuplexColumnToDictionary()
		{
			var duplexStatuses = new Dictionary<string, DuplexStatus>();
			for (var i = 0; i < duplexGetter.Keys.Length; i++)
			{
				var key = Convert.ToString(duplexGetter.Keys[i]);
				var duplexStatus = (DuplexStatus)Convert.ToInt32(duplexGetter.DuplexStatuses[i]);

				duplexStatuses[key] = duplexStatus;
			}

			return duplexStatuses;
		}

		private sealed class DuplexGetter
		{
			private readonly SLProtocol protocol;

			public DuplexGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] DuplexStatuses { get; private set; }

			public void Load()
			{
				var columnsToGet = new uint[] { Parameter.Dot3stats.Idx.dot3stats_index, Parameter.Dot3stats.Idx.dot3stats_duplexstatus };

				var tableData = protocol.GetColumns(Parameter.Dot3stats.tablePid, columnsToGet);

				Keys = (object[])tableData[0];
				DuplexStatuses = (object[])tableData[1];
			}
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

		private class IfxTableSetter
		{
			private readonly SLProtocol protocol;

			public IfxTableSetter(SLProtocol protocol)
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