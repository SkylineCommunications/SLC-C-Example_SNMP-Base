namespace Skyline.Protocol.IfTable
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Scripting;
	using Skyline.DataMiner.Utils.Interfaces;
	using Skyline.DataMiner.Utils.Protocol.Extension;
	using Skyline.DataMiner.Utils.Rates.Protocol;
	using Skyline.DataMiner.Utils.SafeConverters;
	using Skyline.DataMiner.Utils.SNMP;
	using Skyline.Protocol.Interface;

	public class IfTableTimeoutProcessor
	{
		private const int GroupId = 1000;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);

		private readonly SLProtocol protocol;

		private readonly IfTableGetter iftableGetter;
		private readonly IfTableSetter iftableSetter;

		public IfTableTimeoutProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			iftableGetter = new IfTableGetter(protocol);
			iftableGetter.Load();

			iftableSetter = new IfTableSetter(protocol);
		}

		public void ProcessTimeout()
		{
			SnmpDeltaHelper snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratecalculationsmethod);

			for (int i = 0; i < iftableGetter.Keys.Length; i++)
			{
				string key = Convert.ToString(iftableGetter.Keys[i]);
				string serializedIfRateData = Convert.ToString(iftableGetter.IfRateData[i]);

				InterfaceData32 rateData = InterfaceData32.FromJsonString(serializedIfRateData, MinDelta, MaxDelta);

				rateData.BitrateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.BitrateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.UnicastrateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.UnicastrateOut.BufferDelta(snmpDeltaHelper, key);

				iftableSetter.SetColumnsData[Parameter.Iftable.tablePid].Add(key);
				iftableSetter.SetColumnsData[Parameter.Iftable.Pid.iftableifratedata].Add(rateData.ToJsonString());
			}
		}

		public void UpdateProtocol()
		{
			iftableSetter.SetColumns();
		}

		private class IfTableGetter
		{
			private readonly SLProtocol protocol;

			public IfTableGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] IfRateData { get; private set; }

			public void Load()
			{
				uint[] columnsToGet = new uint[]
				{
					Parameter.Iftable.Idx.iftableifindex,
					Parameter.Iftable.Idx.iftableifratedata,
				};

				object[] tableData = protocol.GetColumns(Parameter.Iftable.tablePid, columnsToGet);

				Keys = (object[])tableData[0];
				IfRateData = (object[])tableData[1];
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
				{ Parameter.Iftable.Pid.iftableifratedata, new List<object>() },
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

		private readonly SLProtocol protocol;

		private readonly IfTableGetter iftableGetter;
		private readonly IfTableSetter iftableSetter;
		private readonly DuplexGetter duplexGetter;

		public IfTableProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			iftableGetter = new IfTableGetter(protocol);
			iftableGetter.Load();
			duplexGetter = new DuplexGetter(protocol);
			duplexGetter.Load();

			iftableSetter = new IfTableSetter(protocol);
		}

		public void ProcessData()
		{
			SnmpDeltaHelper snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratecalculationsmethod);

			Dictionary<string, DuplexStatus> duplexStatuses = ConvertDuplexColumnToDictionary();

			for (int i = 0; i < iftableGetter.Keys.Length; i++)
			{
				// Key
				string key = Convert.ToString(iftableGetter.Keys[i]);
				iftableSetter.SetColumnsData[Parameter.Iftable.tablePid].Add(key);

				// Rates
				ProcessRates(snmpDeltaHelper, i, out double bitrateIn, out double bitrateOut);

				// Utilization
				ProcessUtilization(duplexStatuses, i, key, bitrateIn, bitrateOut);
			}

			if (iftableGetter.IsSnmpAgentRestarted)
			{
				iftableSetter.SetParamsData[Parameter.iftablesnmpagentrestartflag] = 0;
			}
		}

		public void UpdateProtocol()
		{
			iftableSetter.SetColumns();
			iftableSetter.SetParams();
		}

		private static double CalculateRate(string key, uint count, SnmpDeltaHelper snmpDeltaHelper, SnmpRate32 snmpRateHelper)
		{
			double rate = snmpRateHelper.Calculate(snmpDeltaHelper, count, key);

			return rate;
		}

		private static double CalculateBitRate(string key, uint octectCount, SnmpDeltaHelper snmpDeltaHelper, SnmpRate32 snmpRateHelper)
		{
			double octetRate = CalculateRate(key, octectCount, snmpDeltaHelper, snmpRateHelper);
			double bitRate = octetRate > 0 ? octetRate * 8 : octetRate;

			return bitRate;
		}

		private void ProcessRates(SnmpDeltaHelper snmpDeltaHelper, int getPosition, out double bitrateIn, out double bitrateOut)
		{
			string key = Convert.ToString(iftableGetter.Keys[getPosition]);

			string serializedIfRateData = Convert.ToString(iftableGetter.RateData[getPosition]);
			InterfaceData32 rateData = InterfaceData32.FromJsonString(serializedIfRateData, MinDelta, MaxDelta);

			string discontinuityTime = Convert.ToString(iftableGetter.Discontinuity[getPosition]);
			bool discontinuity = Interface.HasDiscontinuity(discontinuityTime, rateData.DiscontinuityTime);

			if (iftableGetter.IsSnmpAgentRestarted || discontinuity)
			{
				rateData.BitrateIn = SnmpRate32.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.BitrateOut = SnmpRate32.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.UnicastrateIn = SnmpRate32.FromJsonString(String.Empty, MinDelta, MaxDelta);
				rateData.UnicastrateOut = SnmpRate32.FromJsonString(String.Empty, MinDelta, MaxDelta);
			}

			uint octetsIn = SafeConvert.ToUInt32(Convert.ToDouble(iftableGetter.OctetsIn[getPosition]));
			bitrateIn = CalculateBitRate(key, octetsIn, snmpDeltaHelper, rateData.BitrateIn);

			uint octetsOut = SafeConvert.ToUInt32(Convert.ToDouble(iftableGetter.OctetsOut[getPosition]));
			bitrateOut = CalculateBitRate(key, octetsOut, snmpDeltaHelper, rateData.BitrateOut);

			uint unicastPktsIn = SafeConvert.ToUInt32(Convert.ToDouble(iftableGetter.UcastPktsIn[getPosition]));
			double unicastrateIn = CalculateRate(key, unicastPktsIn, snmpDeltaHelper, rateData.UnicastrateIn);

			uint unicastPktsOut = SafeConvert.ToUInt32(Convert.ToDouble(iftableGetter.UcastPktsOut[getPosition]));
			double unicastrateOut = CalculateRate(key, unicastPktsOut, snmpDeltaHelper, rateData.UnicastrateOut);

			iftableSetter.SetColumnsData[Parameter.Iftable.Pid.iftableifinbitrate].Add(bitrateIn);
			iftableSetter.SetColumnsData[Parameter.Iftable.Pid.iftableifoutbitrate].Add(bitrateOut);

			iftableSetter.SetColumnsData[Parameter.Iftable.Pid.iftableifinunicastrate_1023].Add(unicastrateIn);
			iftableSetter.SetColumnsData[Parameter.Iftable.Pid.iftableifoutunicastrate_1024].Add(unicastrateOut);

			iftableSetter.SetColumnsData[Parameter.Iftable.Pid.iftableifratedata].Add(rateData.ToJsonString());
		}

		private void ProcessUtilization(Dictionary<string, DuplexStatus> duplexStatuses, int getPosition, string key, double bitrateIn, double bitrateOut)
		{
			double speedValue = GetSpeedValue(getPosition);

			DuplexStatus duplexStatus = duplexStatuses.ContainsKey(key)
				? duplexStatuses[key]
				: DuplexStatus.NotInitialized;

			double utilization = Interface.CalculateUtilization(bitrateIn, bitrateOut, speedValue, duplexStatus);

			iftableSetter.SetColumnsData[Parameter.Iftable.Pid.iftableifbandwidthutilization].Add(utilization);
		}

		private double GetSpeedValue(int getPosition)
		{
			uint speedInTable = SafeConvert.ToUInt32(Convert.ToDouble(iftableGetter.Speed[getPosition]));

			return speedInTable == UInt32.MaxValue
								? -1.0
								: Convert.ToDouble(speedInTable);
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

		private class IfTableGetter
		{
			private readonly SLProtocol protocol;

			public IfTableGetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public object[] Keys { get; private set; }

			public object[] OctetsIn { get; private set; }

			public object[] OctetsOut { get; private set; }

			public object[] UcastPktsIn { get; private set; }

			public object[] UcastPktsOut { get; private set; }

			public object[] DiscardsIn { get; private set; }

			public object[] DiscardsOut { get; private set; }

			public object[] ErrorsIn { get; private set; }

			public object[] ErrorsOut { get; private set; }

			public object[] UnknownIn { get; private set; }

			public object[] Speed { get; private set; }

			public object[] Discontinuity { get; private set; }

			public object[] RateData { get; private set; }

			public bool IsSnmpAgentRestarted { get; private set; }

			public void Load()
			{
				IsSnmpAgentRestarted = Convert.ToBoolean(protocol.GetParameter(Parameter.iftablesnmpagentrestartflag));
				LoadIfTable();
				LoadIfXTable();
			}

			private void LoadIfTable()
			{
				uint[] columnsToGet = new uint[]
				{
					Parameter.Iftable.Idx.iftableifindex_1001,
					Parameter.Iftable.Idx.iftableifinoctets_1010,
					Parameter.Iftable.Idx.iftableifoutoctets_1015,
					Parameter.Iftable.Idx.iftableifspeed_1005,
					Parameter.Iftable.Idx.iftableifratedata_1022,
					Parameter.Iftable.Idx.iftableifinucastpkts_1011,
					Parameter.Iftable.Idx.iftableifoutucastpkts_1016,
					Parameter.Iftable.Idx.iftableifindiscards_1012,
					Parameter.Iftable.Idx.iftableifoutdiscards_1017,
					Parameter.Iftable.Idx.iftableifinerrors_1013,
					Parameter.Iftable.Idx.iftableifouterrors_1018,
					Parameter.Iftable.Idx.iftableifinunknownprotos_1014,
				};

				object[] ifTableData = protocol.GetColumns(Parameter.Iftable.tablePid, columnsToGet);
				Keys = (object[])ifTableData[0];
				OctetsIn = (object[])ifTableData[1];
				OctetsOut = (object[])ifTableData[2];
				Speed = (object[])ifTableData[3];
				RateData = (object[])ifTableData[4];
				UcastPktsIn = (object[])ifTableData[5];
				UcastPktsOut = (object[])ifTableData[6];
				DiscardsIn = (object[])ifTableData[7];
				DiscardsOut = (object[])ifTableData[8];
				ErrorsIn = (object[])ifTableData[9];
				ErrorsOut = (object[])ifTableData[10];
				UnknownIn = (object[])ifTableData[11];
				Discontinuity = new object[Keys.Length];    // Will be filled in via LoadIfXTable
			}

			private void LoadIfXTable()
			{
				uint[] columnsToGet = new uint[]
				{
					Parameter.Ifxtable.Idx.ifxtableifindex,
					Parameter.Ifxtable.Idx.ifxtableifcounterdiscontinuitytime,
				};

				object[] ifXTableData = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);
				object[] ifXTableKeys = (object[])ifXTableData[0];
				object[] ifXTableDiscontinuities = (object[])ifXTableData[1];

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

		private class IfTableSetter
		{
			private readonly SLProtocol protocol;

			public IfTableSetter(SLProtocol protocol)
			{
				this.protocol = protocol;
			}

			public Dictionary<int, List<object>> SetColumnsData { get; } = new Dictionary<int, List<object>>
			{
				{ Parameter.Iftable.tablePid, new List<object>() },
				{ Parameter.Iftable.Pid.iftableifinbitrate_1019, new List<object>() },
				{ Parameter.Iftable.Pid.iftableifoutbitrate_1020, new List<object>() },
				{ Parameter.Iftable.Pid.iftableifbandwidthutilization_1021, new List<object>() },
				{ Parameter.Iftable.Pid.iftableifratedata_1022, new List<object>() },
				{ Parameter.Iftable.Pid.iftableifinunicastrate_1023, new List<object>() },
				{ Parameter.Iftable.Pid.iftableifoutunicastrate_1024, new List<object>() },
			};

			internal Dictionary<int, object> SetParamsData { get; } = new Dictionary<int, object>();

			public void SetColumns()
			{
				protocol.SetColumns(SetColumnsData);
			}

			public void SetParams()
			{
				protocol.SetParameters(SetParamsData.Keys.ToArray(), SetParamsData.Values.ToArray());
			}
		}
	}
}