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

		private readonly SLProtocol protocol;

		private readonly IfxTableGetter ifxtableGetter;
		private readonly IfxTableSetter ifxtableSetter;

		public IfxTableTimeoutProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			ifxtableGetter = new IfxTableGetter(protocol);
			ifxtableGetter.Load();

			ifxtableSetter = new IfxTableSetter(protocol);
		}

		public void ProcessTimeout()
		{
			SnmpDeltaHelper snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratecalculationsmethod);

			for (int i = 0; i < ifxtableGetter.Keys.Length; i++)
			{
				string key = Convert.ToString(ifxtableGetter.Keys[i]);
				string serializedIfxRateData = Convert.ToString(ifxtableGetter.IfRateData[i]);

				InterfaceData64 rateData = InterfaceData64.FromJsonString(serializedIfxRateData, MinDelta, MaxDelta);

				rateData.MulticastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.MulticastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.BroadcastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.BroadcastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.HCBitrateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.HCBitrateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.HCUcastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.HCUcastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.HCMulticastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.HCMulticastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.HCBroadcastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.HCBroadcastRateOut.BufferDelta(snmpDeltaHelper, key);

				ifxtableSetter.SetColumnsData[Parameter.Ifxtable.tablePid].Add(key);
				ifxtableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableratesdata].Add(rateData.ToJsonString());
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
				uint[] columnsToGet = new uint[]
				{
					Parameter.Ifxtable.Idx.ifxtableifindex,
					Parameter.Ifxtable.Idx.ifxtableratesdata,
				};

				object[] tableData = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);

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
				{ Parameter.Ifxtable.Pid.ifxtableratesdata, new List<object>() },
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

		private readonly SLProtocol protocol;

		private readonly IfXTableGetter ifxTableGetter;
		private readonly IfxTableSetter ifxTableSetter;
		private readonly DuplexGetter duplexGetter;

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
			SnmpDeltaHelper snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratecalculationsmethod);

			Dictionary<string, DuplexStatus> duplexStatuses = ConvertDuplexColumnToDictionary();

			for (int i = 0; i < ifxTableGetter.Keys.Length; i++)
			{
				// Key
				string key = Convert.ToString(ifxTableGetter.Keys[i]);
				ifxTableSetter.SetColumnsData[Parameter.Ifxtable.tablePid].Add(key);

				// Rates
				ProcessRates(snmpDeltaHelper, i, out double bitrateIn, out double bitrateOut);

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
			string key = Convert.ToString(ifxTableGetter.Keys[getPosition]);

			string serializedIfxRateData = Convert.ToString(ifxTableGetter.RateData[getPosition]);
			InterfaceData64 rateData = InterfaceData64.FromJsonString(serializedIfxRateData, MinDelta, MaxDelta);

			string discontinuityTime = Convert.ToString(ifxTableGetter.Discontinuity[getPosition]);
			bool discontinuity = Interface.HasDiscontinuity(discontinuityTime, rateData.DiscontinuityTime);

			if (ifxTableGetter.IsSnmpAgentRestarted || discontinuity)
			{
				rateData.MulticastRateIn = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.MulticastRateOut = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.BroadcastRateIn = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.BroadcastRateOut = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.HCBitrateIn = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.HCBitrateOut = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.HCUcastRateIn = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.HCUcastRateOut = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.HCMulticastRateIn = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.HCMulticastRateOut = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.HCBroadcastRateIn = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.HCBroadcastRateOut = SnmpRate64.FromJsonString(String.Empty, MinDelta, MaxDelta);
			}

			ulong multicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.MulticastPktsIn[getPosition]));
			double multicastRateIn = CalculateRate(key, multicastPktsIn, snmpDeltaHelper, rateData.MulticastRateIn);

			ulong multicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.MulticastPktsOut[getPosition]));
			double multicastRateOut = CalculateRate(key, multicastPktsOut, snmpDeltaHelper, rateData.MulticastRateOut);

			ulong broadcastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.BroadcastPktsIn[getPosition]));
			double broadcastRateIn = CalculateRate(key, broadcastPktsIn, snmpDeltaHelper, rateData.BroadcastRateIn);

			ulong broadcastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.BroadcastPktsOut[getPosition]));
			double broadcastRateOut = CalculateRate(key, broadcastPktsOut, snmpDeltaHelper, rateData.BroadcastRateOut);

			ulong octetsIn = SafeConvert.ToUInt64(Convert.ToDouble(ifxTableGetter.HCOctetsIn[getPosition]));
			bitrateIn = CalculateBitRate(key, octetsIn, snmpDeltaHelper, rateData.HCBitrateIn);

			ulong octetsOut = SafeConvert.ToUInt64(Convert.ToDouble(ifxTableGetter.HCOctetsOut[getPosition]));
			bitrateOut = CalculateBitRate(key, octetsOut, snmpDeltaHelper, rateData.HCBitrateOut);

			ulong hcUnicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCUcastPktsIn[getPosition]));
			double hcUnicastRateIn = CalculateRate(key, hcUnicastPktsIn, snmpDeltaHelper, rateData.HCUcastRateIn);

			ulong hcUnicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCUcastPktsOut[getPosition]));
			double hcUnicastRateOut = CalculateRate(key, hcUnicastPktsOut, snmpDeltaHelper, rateData.HCUcastRateOut);

			ulong hcMulticastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCMulticastPktsIn[getPosition]));
			double hcMulticastRateIn = CalculateRate(key, hcMulticastPktsIn, snmpDeltaHelper, rateData.HCMulticastRateIn);

			ulong hcMulticastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCMulticastPktsOut[getPosition]));
			double hcMulticastRateOut = CalculateRate(key, hcMulticastPktsOut, snmpDeltaHelper, rateData.HCMulticastRateOut);

			ulong hcBroadcastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCBroadcastPktsIn[getPosition]));
			double hcBroadcastRateIn = CalculateRate(key, hcBroadcastPktsIn, snmpDeltaHelper, rateData.HCBroadcastRateIn);

			ulong hcBroadcastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.HCBroadcastPktsOut[getPosition]));
			double hcBroadcastRateOut = CalculateRate(key, hcBroadcastPktsOut, snmpDeltaHelper, rateData.HCBroadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableinmulticastrate_1125].Add(multicastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableoutmulticastrate_1126].Add(multicastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableinbroadcastrate_1127].Add(broadcastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableoutbroadcastrate_1128].Add(broadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableinbitrate_1121].Add(bitrateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableoutbitrate_1122].Add(bitrateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableinhcucastrate_1129].Add(hcUnicastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableouthcucastrate_1130].Add(hcUnicastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableinhcmulticastrate_1131].Add(hcMulticastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableouthcmulticastrate_1132].Add(hcMulticastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableinhcbroadcastrate_1133].Add(hcBroadcastRateIn);
			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableouthcbroadcastrate_1134].Add(hcBroadcastRateOut);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtableratesdata].Add(rateData.ToJsonString());
		}

		private void ProcessUtilization(Dictionary<string, DuplexStatus> duplexStatuses, int getPosition, string key, double bitrateIn, double bitrateOut)
		{
			double speedValue = GetSpeedValue(getPosition);

			DuplexStatus duplexStatus = duplexStatuses.ContainsKey(key)
				? duplexStatuses[key]
				: DuplexStatus.NotInitialized;

			double utilization = Interface.CalculateUtilization(bitrateIn, bitrateOut, speedValue, duplexStatus);

			ifxTableSetter.SetColumnsData[Parameter.Ifxtable.Pid.ifxtablebandwidthutilization].Add(utilization);
		}

		private double GetSpeedValue(int getPosition)
		{
			uint speedInTable = SafeConvert.ToUInt32(Convert.ToDouble(ifxTableGetter.Speed[getPosition]));

			double speedValueToUse = Convert.ToDouble(speedInTable) * Math.Pow(10, 6);
			return speedValueToUse;
		}

		private Dictionary<string, DuplexStatus> ConvertDuplexColumnToDictionary()
		{
			Dictionary<string, DuplexStatus> duplexStatuses = new Dictionary<string, DuplexStatus>();
			for (int i = 0; i < duplexGetter.Keys.Length; i++)
			{
				string key = Convert.ToString(duplexGetter.Keys[i]);
				DuplexStatus duplexStatus = (DuplexStatus)Convert.ToInt32(duplexGetter.DuplexStatuses[i]);
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
				uint[] columnsToGet = new uint[]
				{
					Parameter.Dot3statstable.Idx.dot3statsindex,
					Parameter.Dot3statstable.Idx.dot3statsduplexstatus,
				};

				object[] tableData = protocol.GetColumns(Parameter.Dot3statstable.tablePid, columnsToGet);

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

				uint[] columnsToGet = new uint[]
				{
					Parameter.Ifxtable.Idx.ifxtableifindex_1101,
					Parameter.Ifxtable.Idx.ifxtableifinmulticastpkts_1103,
					Parameter.Ifxtable.Idx.ifxtableifoutmulticastpkts_1105,
					Parameter.Ifxtable.Idx.ifxtableifinbroadcastpkts_1104,
					Parameter.Ifxtable.Idx.ifxtableifoutbroadcastpkts_1106,
					Parameter.Ifxtable.Idx.ifxtableifhcinoctets_1107,
					Parameter.Ifxtable.Idx.ifxtableifhcoutoctets_1111,
					Parameter.Ifxtable.Idx.ifxtableifhcinucastpkts_1108,
					Parameter.Ifxtable.Idx.ifxtableifhcoutucastpkts_1112,
					Parameter.Ifxtable.Idx.ifxtableifhcinmulticastpkts_1109,
					Parameter.Ifxtable.Idx.ifxtableifhcoutmulticastpkts_1113,
					Parameter.Ifxtable.Idx.ifxtableifhcinbroadcastpkts_1110,
					Parameter.Ifxtable.Idx.ifxtableifhcoutbroadcastpkts_1114,
					Parameter.Ifxtable.Idx.ifxtableifhighspeed_1116,
					Parameter.Ifxtable.Idx.ifxtableifcounterdiscontinuitytime_1120,
					Parameter.Ifxtable.Idx.ifxtableratesdata_1124,
				};

				object[] tableData = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);

				Keys = (object[])tableData[0];
				MulticastPktsIn = (object[])tableData[1];
				MulticastPktsOut = (object[])tableData[2];
				BroadcastPktsIn = (object[])tableData[3];
				BroadcastPktsOut = (object[])tableData[4];
				HCOctetsIn = (object[])tableData[5];
				HCOctetsOut = (object[])tableData[6];
				HCUcastPktsIn = (object[])tableData[7];
				HCUcastPktsOut = (object[])tableData[8];
				HCMulticastPktsIn = (object[])tableData[9];
				HCMulticastPktsOut = (object[])tableData[10];
				HCBroadcastPktsIn = (object[])tableData[11];
				HCBroadcastPktsOut = (object[])tableData[12];
				Speed = (object[])tableData[13];
				Discontinuity = (object[])tableData[14];
				RateData = (object[])tableData[15];
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
				{ Parameter.Ifxtable.Pid.ifxtableinmulticastrate_1125, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableoutmulticastrate_1126, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableinbroadcastrate_1127, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableoutbroadcastrate_1128, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableinbitrate_1121, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableoutbitrate_1122, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableinhcucastrate_1129, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableouthcucastrate_1130, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableinhcmulticastrate_1131, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableouthcmulticastrate_1132, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableinhcbroadcastrate_1133, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableouthcbroadcastrate_1134, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtablebandwidthutilization, new List<object>() },
				{ Parameter.Ifxtable.Pid.ifxtableratesdata, new List<object>() },
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