using System;

using QAction_1000.IfTable;

using Skyline.DataMiner.Scripting;

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
			var ifTableProcessor = new IfTableProcessor(protocol);
			ifTableProcessor.ProcessData();
			ifTableProcessor.UpdateProtocol();
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|ProcessSuccess|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
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
			var ifTableTimeoutProcessor = new IfTableTimeoutProcessor(protocol);
			ifTableTimeoutProcessor.ProcessTimeout();
			ifTableTimeoutProcessor.UpdateProtocol();
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|ProcessTimeout|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}
}