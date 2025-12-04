using System;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.SNMP;

/// <summary>
/// DataMiner QAction Class: After Startup.
/// </summary>
public static class QAction
{
	/// <summary>
	/// The QAction entry point.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocol protocol)
	{
		try
		{
			// Every restart of an element, the method is defaulted back to "Fast" by DataMiner so we only need to change it if we expect 'Accurate'
			CalculationMethod rateCalculationsMethod = (CalculationMethod)Convert.ToInt32(protocol.GetParameter(Parameter.interfacesratecalculationsmethod));
			if (rateCalculationsMethod == CalculationMethod.Accurate)
			{
				SnmpDeltaHelper.UpdateRateDeltaTracking(
					protocol,
					tablePids: new[] { 1000, 1100 },
					CalculationMethod.Accurate);
			}
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}
}