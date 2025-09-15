using System;
using Skyline.DataMiner.Scripting;
using Skyline.Protocol.IfTable;

/// <summary>
/// Represents the ifTable.
/// </summary>
public class IfTable
{
	/// <summary>
	/// QAction entry point when table was successfully polled.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void ProcessTable(SLProtocol protocol)
	{
		try
		{
			var interfaceTableProcessor = new IfTableProcessor(protocol);
			interfaceTableProcessor.ProcessData();
			interfaceTableProcessor.UpdateProtocol();
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|ProcessSuccess|Error: {ex}", LogType.Error, LogLevel.NoLogging);
			ProcessTimeout(protocol);
		}
	}

	/// <summary>
	/// QAction entry point when a timeout occurred while polling the ifTable table.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void ProcessTimeout(SLProtocol protocol)
	{
		try
		{
			var interfaceTableProcessor = new IfTableTimeoutProcessor(protocol);
			interfaceTableProcessor.ProcessTimeout();
			interfaceTableProcessor.UpdateProtocol();
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|ProcessTimeout|Error: {ex}", LogType.Error, LogLevel.NoLogging);
		}
	}
}