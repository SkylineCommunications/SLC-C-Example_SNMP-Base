using System;

using Skyline.DataMiner.Scripting;

/// <summary>
/// DataMiner QAction Class: Interface Table SNMP Sets.
/// </summary>
public class QAction
{
	private const int TriggerIfTable = 1000;
	private const int TriggerIfXTable = 1100;
	private const int TriggerInterfaceMerge = 1191;

	/// <summary>
	/// The QAction entry point.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocol protocol)
	{
		try
		{
			int triggerPid = protocol.GetTriggerParameter();
			string rowKey = protocol.RowKey();
			object value = protocol.GetParameter(Convert.ToInt32(triggerPid));

			switch (triggerPid)
			{
				case Parameter.Write.interfacesstateadminstatus_2055:
					protocol.SetParameters(
						new[] { Parameter.iftablesetinstance, Parameter.Write.iftableifadminstatus_1057 },
						new[] { rowKey, value });

					// Note: We poll the entire table so the bit rate calculation is triggered again using the correct values.
					// Polling just the cell or row could lead to wrong calculations.
					protocol.CheckTrigger(TriggerIfTable);
					break;

				case Parameter.Write.interfacesstatepromiscuousmode_2069:
					protocol.SetParameters(
						new[] { Parameter.ifxtablesetinstance, Parameter.Write.ifxtableifpromiscuousmode_1157 },
						new[] { rowKey, value });

					protocol.CheckTrigger(TriggerIfXTable);
					break;

				case Parameter.Write.interfacesstateuserdescription_2065:
					protocol.SetParameters(
						new[] { Parameter.ifxtablesetinstance, Parameter.Write.ifxtableifalias_1159 },
						new[] { rowKey, value });

					protocol.CheckTrigger(TriggerIfXTable);
					break;

				case Parameter.Write.interfacesstatelinkupdowntrap_2070:
					protocol.SetParameters(
						new[] { Parameter.ifxtablesetinstance, Parameter.Write.ifxtableiflinkupdowntrapenable_1155 },
						new[] { rowKey, value });

					protocol.CheckTrigger(TriggerIfXTable);
					break;

				default:
					protocol.Log(
						$"QA{protocol.QActionID}|Run|QAction triggered by unexpected param '{triggerPid}'",
						LogType.Error,
						LogLevel.NoLogging);
					break;
			}

			protocol.CheckTrigger(TriggerInterfaceMerge);
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|Run|Error: {ex}", LogType.Error, LogLevel.NoLogging);
		}
	}
}