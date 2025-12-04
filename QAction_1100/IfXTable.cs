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

	using SLNetMessages = Skyline.DataMiner.Net.Messages;

	public class IfxTableTimeoutProcessor
	{
		private const int GroupId = 1100;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);

		private readonly IfxTableGetter ifxtableGetter;
		private readonly IfxTableSetter ifxtableSetter;

		private readonly SLProtocol protocol;

		public IfxTableTimeoutProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			ifxtableGetter = new IfxTableGetter(protocol);
			ifxtableGetter.Load();

			ifxtableSetter = new IfxTableSetter(protocol);
		}

		public void ProcessTimeout()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratescalculationsmethod);

			for (var i = 0; i < ifxtableGetter.Keys.Length; i++)
			{
				var key = Convert.ToString(ifxtableGetter.Keys[i]);
				var serializedIfxRateData = Convert.ToString(ifxtableGetter.IfRateData[i]);

				var rateData = InterfaceExtendedRateData.FromJsonString(serializedIfxRateData, MinDelta, MaxDelta);

				rateData.MulticastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.MulticastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.BroadcastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.BroadcastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.HcBitRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.HcBitRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.HcUnicastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.HcUnicastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.HcMulticastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.HcMulticastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.HcBroadcastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.HcBroadcastRateOut.BufferDelta(snmpDeltaHelper, key);

				ifxtableSetter.SetColumnsData[Parameter.Ifxtable.tablePid].Add(key);
				ifxtableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_ratesdata].Add(rateData.ToJsonString());
			}
		}

		public void UpdateProtocol()
		{
			ifxtableSetter.SetColumns();
		}

		private class IfxTableGetter
		{
			private readonly SLProtocol protocol;

			public IfxTableGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] IfRateData { get; private set; }

			public void Load()
			{
				var columnsToGet = new uint[] { Parameter.Ifxtable.Idx.ifxtable_ifindex_1101, Parameter.Ifxtable.Idx.ifxtable_ratesdata };

				var tableData = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);

				Keys = (object[])tableData[0];
				IfRateData = (object[])tableData[1];
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

			var serializedIfxRateData = Convert.ToString(ifxTableGetter.RateData[getPosition]);
			var rateData = InterfaceExtendedRateData.FromJsonString(serializedIfxRateData, MinDelta, MaxDelta);

			var discontinuityTime = Convert.ToString(ifxTableGetter.Discontinuity[getPosition]);
			var discontinuity = Interface.HasDiscontinuity(discontinuityTime, rateData.DiscontinuityTime);

			if (ifxTableGetter.IsSnmpAgentRestarted || discontinuity)
			{
				rateData.MulticastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.MulticastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.BroadcastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.BroadcastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.HcBitRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.HcBitRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.HcUnicastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.HcUnicastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.HcMulticastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.HcMulticastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.HcBroadcastRateIn = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.HcBroadcastRateOut = SnmpRate64.FromJsonString(string.Empty, MinDelta, MaxDelta);
			}

			ulong multicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.MulticastPktsIn[getPosition]));
			var multicastRateIn = CalculateRate(key, multicastPktsIn, snmpDeltaHelper, rateData.MulticastRateIn);

			ulong multicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.MulticastPktsOut[getPosition]));
			var multicastRateOut = CalculateRate(key, multicastPktsOut, snmpDeltaHelper, rateData.MulticastRateOut);

			ulong broadcastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.BroadcastPktsIn[getPosition]));
			var broadcastRateIn = CalculateRate(key, broadcastPktsIn, snmpDeltaHelper, rateData.BroadcastRateIn);

			ulong broadcastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.BroadcastPktsOut[getPosition]));
			var broadcastRateOut = CalculateRate(key, broadcastPktsOut, snmpDeltaHelper, rateData.BroadcastRateOut);

			var octetsIn = SafeConvert.ToUInt64(Convert.ToDouble(ifxTableGetter.HCOctetsIn[getPosition]));
			bitrateIn = CalculateBitRate(key, octetsIn, snmpDeltaHelper, rateData.HcBitRateIn);

			var octetsOut = SafeConvert.ToUInt64(Convert.ToDouble(ifxTableGetter.HCOctetsOut[getPosition]));
			bitrateOut = CalculateBitRate(key, octetsOut, snmpDeltaHelper, rateData.HcBitRateOut);

			ulong hcUnicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCUcastPktsIn[getPosition]));
			var hcUnicastRateIn = CalculateRate(key, hcUnicastPktsIn, snmpDeltaHelper, rateData.HcUnicastRateIn);

			ulong hcUnicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCUcastPktsOut[getPosition]));
			var hcUnicastRateOut = CalculateRate(key, hcUnicastPktsOut, snmpDeltaHelper, rateData.HcUnicastRateOut);

			ulong hcMulticastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCMulticastPktsIn[getPosition]));
			var hcMulticastRateIn = CalculateRate(key, hcMulticastPktsIn, snmpDeltaHelper, rateData.HcMulticastRateIn);

			ulong hcMulticastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCMulticastPktsOut[getPosition]));
			var hcMulticastRateOut = CalculateRate(key, hcMulticastPktsOut, snmpDeltaHelper, rateData.HcMulticastRateOut);

			ulong hcBroadcastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCBroadcastPktsIn[getPosition]));
			var hcBroadcastRateIn = CalculateRate(key, hcBroadcastPktsIn, snmpDeltaHelper, rateData.HcBroadcastRateIn);

			ulong hcBroadcastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCBroadcastPktsOut[getPosition]));
			var hcBroadcastRateOut = CalculateRate(key, hcBroadcastPktsOut, snmpDeltaHelper, rateData.HcBroadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_multicastratein_1125].Add(multicastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_multicastrateout_1126].Add(multicastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_broadcastratein_1127].Add(broadcastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_broadcastrateout_1128].Add(broadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_bitratein_1121].Add(bitrateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_bitrateout_1122].Add(bitrateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcucastratein_1129].Add(hcUnicastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcucastrateout_1130].Add(hcUnicastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcmulticastratein_1131].Add(hcMulticastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcmulticastrateout_1132].Add(hcMulticastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcbroadcastratein_1133].Add(hcBroadcastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_hcbroadcastrateout_1134].Add(hcBroadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtable_ratesdata].Add(rateData.ToJsonString());
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
			var speedInTable = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.Speed[getPosition]));

			var speedValueToUse = Convert.ToDouble(speedInTable) * Math.Pow(10, 6);
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

		private class DuplexGetter
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
					Parameter.Ifxtable.Idx.ifxtable_ifindex_1101,
					Parameter.Ifxtable.Idx.ifxtable_ifmulticastpktsin_1103,
					Parameter.Ifxtable.Idx.ifxtable_ifmulticastpktsout_1104,
					Parameter.Ifxtable.Idx.ifxtable_ifbroadcastpktsin_1105,
					Parameter.Ifxtable.Idx.ifxtable_ifbroadcastpktsout_1106,
					Parameter.Ifxtable.Idx.ifxtable_ifhcoctetsin_1107,
					Parameter.Ifxtable.Idx.ifxtable_ifhcoctetsout_1108,
					Parameter.Ifxtable.Idx.ifxtable_ifhcucastpktsin_1109,
					Parameter.Ifxtable.Idx.ifxtable_ifhcucastpktsout_1110,
					Parameter.Ifxtable.Idx.ifxtable_ifhcmulticastpktsin_1111,
					Parameter.Ifxtable.Idx.ifxtable_ifhcmulticastpktsout_1112,
					Parameter.Ifxtable.Idx.ifxtable_ifhcbroadcastpktsin_1113,
					Parameter.Ifxtable.Idx.ifxtable_ifhcbroadcastpktsout_1114,
					Parameter.Ifxtable.Idx.ifxtable_ifhighspeed_1116,
					Parameter.Ifxtable.Idx.ifxtable_ifcounterdiscontinuitytime_1120,
					Parameter.Ifxtable.Idx.ifxtable_ratesdata_1124,
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
				{ Parameter.Ifxtable.Pid.ifxtable_multicastratein_1125, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_multicastrateout_1126, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_broadcastratein_1127, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_broadcastrateout_1128, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_bitratein_1121, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_bitrateout_1122, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcucastratein_1129, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcucastrateout_1130, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcmulticastratein_1131, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcmulticastrateout_1132, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcbroadcastratein_1133, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtable_hcbroadcastrateout_1134, new List<object>() },
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