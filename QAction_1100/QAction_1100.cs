using System;

using QAction_1100.IfxTable;

using Skyline.DataMiner.Scripting;

/// <summary>
/// Represents the ifXTable.
/// </summary>
public class IfXTable
{
	/// <summary>
	/// QAction entry point when table was successfully polled.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void ProcessTable(SLProtocol protocol)
	{
		try
		{
			var ifXTableProcessor = new IfXTableProcessor(protocol);
			ifXTableProcessor.ProcessData();
			ifXTableProcessor.UpdateProtocol();
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
			var ifXTableTimeoutProcessor = new IfXTableTimeoutProcessor(protocol);
			ifXTableTimeoutProcessor.ProcessTimeout();
			ifXTableTimeoutProcessor.UpdateProtocol();
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|{protocol.GetTriggerParameter()}|ProcessTimeout|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}
}