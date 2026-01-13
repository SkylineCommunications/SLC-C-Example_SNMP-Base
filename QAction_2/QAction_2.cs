using System;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.SNMP;
using Skyline.Protocol.Api.Helpers;

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
            CalculationMethod rateCalculationsMethod = (CalculationMethod)Convert.ToInt32(protocol.GetParameter(Parameter.interfacesratescalculationsmethod));
            if (rateCalculationsMethod == CalculationMethod.Accurate)
            {
                SnmpDeltaHelper.UpdateRateDeltaTracking(
                    protocol,
                    tablePids: new[] { Parameter.Iftable.tablePid, Parameter.Ifxtable.tablePid },
                    CalculationMethod.Accurate);
            }

            InterfacesTableHelper.InitialiseCustomDescriptionColumn(protocol);
        }
        catch (Exception ex)
        {
            protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
        }
    }
}