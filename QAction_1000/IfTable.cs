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

	using SLNetMessages = Skyline.DataMiner.Net.Messages;

	public class IfTableTimeoutProcessor
	{
		private const int GroupId = 1000;
		private static readonly TimeSpan MinDelta = new TimeSpan(0, 0, 5);
		private static readonly TimeSpan MaxDelta = new TimeSpan(0, 10, 0);

		private readonly IfTableGetter interfaceTableGetter;
		private readonly IfTableSetter interfaceTableSetter;

		private readonly SLProtocol protocol;

		public IfTableTimeoutProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			interfaceTableGetter = new IfTableGetter(protocol);
			interfaceTableGetter.Load();

			interfaceTableSetter = new IfTableSetter(protocol);
		}

		public void ProcessTimeout()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratecalculationsmethod);

			for (var i = 0; i < interfaceTableGetter.Keys.Length; i++)
			{
				var key = Convert.ToString(interfaceTableGetter.Keys[i]);
				var serializedIfRateData = Convert.ToString(interfaceTableGetter.IfRateData[i]);

				var rateData = InterfaceRateData.FromJsonString(serializedIfRateData, MinDelta, MaxDelta);

				rateData.BitRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.BitRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.UnicastRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.UnicastRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.DiscardRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.DiscardRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.ErrorRateIn.BufferDelta(snmpDeltaHelper, key);
				rateData.ErrorRateOut.BufferDelta(snmpDeltaHelper, key);

				rateData.UnknownProtocolsRateIn.BufferDelta(snmpDeltaHelper, key);

				interfaceTableSetter.SetColumnsData[Parameter.Iftable.tablePid].Add(key);
				interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_ratesdata].Add(rateData.ToJsonString());
			}
		}

		public void UpdateProtocol()
		{
			interfaceTableSetter.SetColumns();
		}

		private sealed class IfTableGetter
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
				var columnsToGet = new uint[] { Parameter.Iftable.Idx.iftable_ifindex, Parameter.Iftable.Idx.iftable_ratesdata };

				var tableData = protocol.GetColumns(Parameter.Iftable.tablePid, columnsToGet);

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
		private readonly DuplexGetter duplexGetter;

		private readonly IfTableGetter interfaceTableGetter;
		private readonly IfTableSetter interfaceTableSetter;

		private readonly SLProtocol protocol;

		public IfTableProcessor(SLProtocol protocol)
		{
			this.protocol = protocol;

			interfaceTableGetter = new IfTableGetter(protocol);
			interfaceTableGetter.Load();
			duplexGetter = new DuplexGetter(protocol);
			duplexGetter.Load();

			interfaceTableSetter = new IfTableSetter(protocol);
		}

		public void ProcessData()
		{
			var snmpDeltaHelper = new SnmpDeltaHelper(protocol, GroupId, Parameter.interfacesratecalculationsmethod);

			var duplexStatuses = ConvertDuplexColumnToDictionary();

			for (var i = 0; i < interfaceTableGetter.Keys.Length; i++)
			{
				// Key
				var key = Convert.ToString(interfaceTableGetter.Keys[i]);
				interfaceTableSetter.SetColumnsData[Parameter.Iftable.tablePid].Add(key);

				// Rates
				ProcessRates(snmpDeltaHelper, i, out var bitrateIn, out var bitrateOut);

				// Utilization
				ProcessUtilization(duplexStatuses, i, key, bitrateIn, bitrateOut);
			}

			if (interfaceTableGetter.IsSnmpAgentRestarted)
			{
				interfaceTableSetter.SetParamsData[Parameter.iftablesnmpagentrestartflag] = 0;
			}
		}

		public void UpdateProtocol()
		{
			interfaceTableSetter.SetColumns();
			interfaceTableSetter.SetParams();
		}

		private static double CalculateRate(string key, uint count, SnmpDeltaHelper snmpDeltaHelper, SnmpRate32 snmpRateHelper)
		{
			var rate = snmpRateHelper.Calculate(snmpDeltaHelper, count, key);

			return rate;
		}

		private static double CalculateBitRate(string key, uint octetCount, SnmpDeltaHelper snmpDeltaHelper, SnmpRate32 snmpRateHelper)
		{
			var octetRate = CalculateRate(key, octetCount, snmpDeltaHelper, snmpRateHelper);
			var bitRate = octetRate > 0 ? octetRate * 8 : octetRate;

			return bitRate;
		}

		private void ProcessRates(SnmpDeltaHelper snmpDeltaHelper, int getPosition, out double bitRateIn, out double bitRateOut)
		{
			var key = Convert.ToString(interfaceTableGetter.Keys[getPosition]);

			var serializedIfRateData = Convert.ToString(interfaceTableGetter.RateData[getPosition]);
			var rateData = InterfaceRateData.FromJsonString(serializedIfRateData, MinDelta, MaxDelta);

			var discontinuityTime = Convert.ToString(interfaceTableGetter.Discontinuity[getPosition]);
			var discontinuity = Interface.HasDiscontinuity(discontinuityTime, rateData.DiscontinuityTime);

			if (interfaceTableGetter.IsSnmpAgentRestarted || discontinuity)
			{
				rateData.BitRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.BitRateOut = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.UnicastRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.UnicastRateOut = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.DiscardRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.DiscardRateOut = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.ErrorRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.ErrorRateOut = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
				rateData.UnknownProtocolsRateIn = SnmpRate32.FromJsonString(string.Empty, MinDelta, MaxDelta);
			}

			var octetsIn = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.OctetsIn[getPosition]));
			bitRateIn = CalculateBitRate(key, octetsIn, snmpDeltaHelper, rateData.BitRateIn);

			var octetsOut = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.OctetsOut[getPosition]));
			bitRateOut = CalculateBitRate(key, octetsOut, snmpDeltaHelper, rateData.BitRateOut);

			var unicastIn = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.UnicastPacketsIn[getPosition]));
			var unicastRateIn = CalculateRate(key, unicastIn, snmpDeltaHelper, rateData.UnicastRateIn);

			var unicastOut = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.UnicastPacketsOut[getPosition]));
			var unicastRateOut = CalculateRate(key, unicastOut, snmpDeltaHelper, rateData.UnicastRateOut);

			var discardsIn = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.DiscardsIn[getPosition]));
			var discardRateIn = CalculateRate(key, discardsIn, snmpDeltaHelper, rateData.DiscardRateIn);

			var discardsOut = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.DiscardsOut[getPosition]));
			var discardRateOut = CalculateRate(key, discardsOut, snmpDeltaHelper, rateData.DiscardRateOut);

			var errorsIn = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.ErrorsIn[getPosition]));
			var errorRateIn = CalculateRate(key, errorsIn, snmpDeltaHelper, rateData.ErrorRateIn);

			var errorsOut = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.ErrorsOut[getPosition]));
			var errorRateOut = CalculateRate(key, errorsOut, snmpDeltaHelper, rateData.ErrorRateOut);

			var unknownProtocolsIn = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.UnknownIn[getPosition]));
			var unknownProtocolRateIn = CalculateRate(key, unknownProtocolsIn, snmpDeltaHelper, rateData.UnknownProtocolsRateIn);

			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_bitratein_1019].Add(bitRateIn);
			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_bitrateout_1020].Add(bitRateOut);

			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_unicastratein_1021].Add(unicastRateIn);
			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_unicastrateout_1022].Add(unicastRateOut);

			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_discardratein_1023].Add(discardRateIn);
			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_discardrateout_1024].Add(discardRateOut);

			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_errorratein_1025].Add(errorRateIn);
			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_errorrateout_1026].Add(errorRateOut);

			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_unknownprotocolratein_1027].Add(unknownProtocolRateIn);

			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_ratesdata].Add(rateData.ToJsonString());
		}

		private void ProcessUtilization(Dictionary<string, DuplexStatus> duplexStatuses, int getPosition, string key, double bitrateIn, double bitrateOut)
		{
			var speedValue = GetSpeedValue(getPosition);

			var duplexStatus = duplexStatuses.TryGetValue(key, out var status)
				? status
				: DuplexStatus.NotInitialized;

			var utilization = Interface.CalculateUtilization(bitrateIn, bitrateOut, speedValue, duplexStatus);

			interfaceTableSetter.SetColumnsData[Parameter.Iftable.Pid.iftable_bandwidthutilization].Add(utilization);
		}

		private double GetSpeedValue(int getPosition)
		{
			var speedInTable = SafeConvert.ToUInt32(Convert.ToDouble(interfaceTableGetter.Speed[getPosition]));

			return speedInTable == uint.MaxValue
				? -1.0
				: Convert.ToDouble(speedInTable);
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
				var ifTableColumnsToGetIDXs = new uint[]
				{
					Parameter.Iftable.Idx.iftable_ifindex,
					Parameter.Iftable.Idx.iftable_ifoctetsin_1010,
					Parameter.Iftable.Idx.iftable_ifoctetsout_1011,
					Parameter.Iftable.Idx.iftable_ifspeed_1005,
					Parameter.Iftable.Idx.iftable_ifucastpktsin_1012,
					Parameter.Iftable.Idx.iftable_ifucastpktsout_1013,
					Parameter.Iftable.Idx.iftable_ifdiscardsin_1014,
					Parameter.Iftable.Idx.iftable_ifdiscardsout_1015,
					Parameter.Iftable.Idx.iftable_iferrorsin_1016,
					Parameter.Iftable.Idx.iftable_iferrorsout_1017,
					Parameter.Iftable.Idx.iftable_ifunknownprotosin_1018,
					Parameter.Iftable.Idx.iftable_ratesdata_1029,
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
				RateData = (object[])ifTableColumns[11];

				Discontinuity = new object[Keys.Length]; // Will be filled in via LoadIfXTable
			}

			private void LoadIfXTable()
			{
				var columnsToGet = new uint[] { Parameter.Ifxtable.Idx.ifxtable_ifindex_1101, Parameter.Ifxtable.Idx.ifxtable_ifcounterdiscontinuitytime_1120 };

				var interfaceExtendedTableData = protocol.GetColumns(Parameter.Ifxtable.tablePid, columnsToGet);
				var interfaceExtendedTableKeys = (object[])interfaceExtendedTableData[0];
				var discontinuities = (object[])interfaceExtendedTableData[1];

				for (var i = 0; i < interfaceExtendedTableKeys.Length; i++)
				{
					var position = Array.IndexOf(Keys, interfaceExtendedTableKeys[i]);
					if (position > -1)
					{
						Discontinuity[position] = discontinuities[i];
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
				{ Parameter.Iftable.Pid.iftable_bitratein_1019, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_bitrateout_1020, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_bandwidthutilization_1028, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_ratesdata_1029, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_unicastratein, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_unicastrateout_1022, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_discardratein_1023, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_discardrateout_1024, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_errorratein_1025, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_errorrateout_1026, new List<object>() },
				{ Parameter.Iftable.Pid.iftable_unknownprotocolratein_1027, new List<object>() },
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