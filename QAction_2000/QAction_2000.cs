using System;

using Skyline.DataMiner.Scripting;

/// <summary>
/// DataMiner QAction Class: Interface Table SNMP Sets.
/// </summary>
public class QAction
{
	private const int TriggerIfTable = 1000;
	private const int TriggerIfXTable = 1100;
	private const int TriggerInterfaceMerge = 1991;

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
				case Parameter.Write.interfacesadminstatus:
					protocol.SetParameters(
						new[] { Parameter.iftablesetinstance, Parameter.Write.iftable_ifadminstatus },
						new[] { rowKey, value });

					/* Note: Since changing the admin status has impact on the whole row, we want to poll the whole row rather than just the cell.
					 * However, if we poll the row via snmpSetAndGet option, counters will be updated but our buffered RatesData will not be updated which would lead to wrong calculations next polling cycle.
					 * So we keep the snmpSetAndGet only to get the cell for quick update, and we trigger a full table poll to update the RatesData correctly.
					 */
					protocol.CheckTrigger(TriggerIfTable);
					break;

				case Parameter.Write.interfacespromiscuousmode:
					protocol.SetParameters(
						new[] { Parameter.ifxtablesetinstance, Parameter.Write.ifxtable_ifpromiscuousmode },
						new[] { rowKey, value });

					protocol.CheckTrigger(TriggerIfXTable);
					break;

				case Parameter.Write.interfacesalias:
					protocol.SetParameters(
						new[] { Parameter.ifxtablesetinstance, Parameter.Write.ifxtable_ifalias },
						new[] { rowKey, value });

					/* This is just an Alias, won't have impact on rates and counters -> no need to poll the entire table. */
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
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}
}