using System;
using System.Collections.Generic;

using Skyline.DataMiner.Scripting;
using Skyline.DataMiner.Utils.Protocol.Extension;
using Skyline.DataMiner.Utils.SNMP;

/// <summary>
/// DataMiner SysUptime Class.
/// </summary>
public static class SysUptime
{
    /// <summary>
    /// The QAction entry point in case of timeout.
    /// </summary>
    /// <param name="protocol">Link with SLProtocol process.</param>
    public static void ProcessTimeout(SLProtocol protocol)
    {
        try
        {
            object[] getParams = (object[])protocol.GetParameters(new uint[] { Parameter.sysuptimebuffer });
            string sysUptimeBuffer = Convert.ToString(getParams[0]);

            var paramsToSet = new Dictionary<int, object>();

            SnmpDeltaHelper snmpDeltaHelper = new SnmpDeltaHelper(protocol, groupId: 1);
            SnmpHelper snmpHelper = SnmpHelper.FromJsonString(sysUptimeBuffer, snmpDeltaHelper);
            snmpHelper.BufferDelta();

            paramsToSet.Add(Parameter.sysuptimebuffer, snmpHelper.ToJsonString());

            protocol.SetParameters(paramsToSet);
        }
        catch (Exception ex)
        {
            protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
        }
    }

    /// <summary>
    /// The QAction entry point in case of successful group execution.
    /// </summary>
    /// <param name="protocol">Link with SLProtocol process.</param>
    public static void ProcessNewValue(SLProtocol protocol)
    {
        try
        {
            object[] getParams = (object[])protocol.GetParameters(new uint[] { Parameter.sysuptimebuffer, Parameter.sysuptime });
            string sysUptimeBuffer = Convert.ToString(getParams[0]);
            double sysUptime = Convert.ToDouble(getParams[1]);

            var paramsToSet = new Dictionary<int, object>();

            SnmpDeltaHelper snmpDeltaHelper = new SnmpDeltaHelper(protocol, groupId: 1);
            SnmpHelper snmpHelper = SnmpHelper.FromJsonString(sysUptimeBuffer, snmpDeltaHelper);
            if (snmpHelper.IsSnmpAgentRestarted(sysUptime))
            {
                paramsToSet.Add(Parameter.iftablesnmpagentrestartflag, 1);
                paramsToSet.Add(Parameter.ifxtablesnmpagentrestartflag, 1);
            }

            paramsToSet.Add(Parameter.sysuptimebuffer, snmpHelper.ToJsonString());

            protocol.SetParameters(paramsToSet);
        }
        catch (Exception ex)
        {
            protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|Run|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
        }
    }
}