using System;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;
using Skyline.Protocol.QActionHelpers;

/// <summary>
/// DataMiner QAction Class: Interface Table SNMP Sets.
/// </summary>
public class QAction
{
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
					 * However, if we poll the row via snmpSetAndGet option,
					 *	counters will be updated but our buffered RatesData will not be updated which would lead to wrong calculations next polling cycle.
					 * So we keep the snmpSetAndGet only to get the cell for quick update, and we trigger a full table poll to update the RatesData correctly.
					 * We poll both tables as we don't know if the 32bit or 64bit counters are used on that interface.
					 */
					protocol.RunAction(Actions.IfTable_ExecuteNext);
					protocol.RunAction(Actions.IfXTable_ExecuteNext);
					break;

				case Parameter.Write.interfacespromiscuousmode:
					protocol.SetParameters(
						new[] { Parameter.ifxtablesetinstance, Parameter.Write.ifxtable_ifpromiscuousmode },
						new[] { rowKey, value });

					/* Updating the Promiscuous Mode won't have much impact on other interface values -> no need to poll the entire table. */
					break;

				case Parameter.Write.interfacesalias:
					protocol.SetParameters(
						new[] { Parameter.ifxtablesetinstance, Parameter.Write.ifxtable_ifalias },
						new[] { rowKey, value });

					/* Updating the Alias won't have impact on any other interface value -> no need to poll the entire table. */
					break;

				default:
					protocol.Log(
						$"QA{protocol.QActionID}|Run|QAction triggered by unexpected param '{triggerPid}'",
						LogType.Error,
						LogLevel.NoLogging);
					break;
			}

			protocol.RunAction(Actions.InterfacesTables_Merge);
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}
}