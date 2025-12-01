using System;

using Skyline.DataMiner.Scripting;
using Skyline.Protocol.IfxTable;

/// <summary>
/// Represents the ifxTable.
/// </summary>
public class IfxTable
{
	/// <summary>
	/// QAction entry point when table was successfully polled.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void ProcessTable(SLProtocol protocol)
	{
		try
		{
			IfxTableProcessor interfacexTableProcessor = new IfxTableProcessor(protocol);
			interfacexTableProcessor.ProcessData();
			interfacexTableProcessor.UpdateProtocol();
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|ProcessSuccess|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
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
			IfxTableTimeoutProcessor interfacexTableProcessor = new IfxTableTimeoutProcessor(protocol);
			interfacexTableProcessor.ProcessTimeout();
			interfacexTableProcessor.UpdateProtocol();
		}
		catch (Exception ex)
		{
			protocol.Log($"QA{protocol.QActionID}|ProcessTimeout|Exception thrown:{Environment.NewLine}{ex}", LogType.Error, LogLevel.NoLogging);
		}
	}
}