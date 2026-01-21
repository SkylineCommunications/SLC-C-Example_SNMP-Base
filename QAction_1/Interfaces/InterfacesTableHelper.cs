namespace Skyline.Protocol.Interfaces
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	using Skyline.DataMiner.Scripting;
	using Skyline.DataMiner.Utils.Protocol.Extension;

	using SLNetMessages = DataMiner.Net.Messages;

	public static class InterfacesTableHelper
	{
		/// <summary>
		/// Sets exception values to empty cells in custom description column.
		/// </summary>
		/// <param name="protocol">Link to SLProtocol process.</param>
		public static void InitialiseCustomDescriptionColumn(SLProtocol protocol)
		{
			var pairsToSetException = BuildCustomDescriptionPairsToSetException(protocol);

			var columnValues = new object[]
			{
				new List<object>(pairsToSetException.Keys).ToArray(),
				new List<object>(pairsToSetException.Values).ToArray(),
			};

			protocol.NotifyProtocol((int)SLNetMessages.NotifyType.NT_FILL_ARRAY_WITH_COLUMN, new object[] { Parameter.Interfaces.tablePid, Parameter.Interfaces.Pid.interfacescustomdescription_2017 }, columnValues);
		}

		private static Dictionary<string, string> BuildCustomDescriptionPairsToSetException(SLProtocol protocol)
		{
			var columnIndexes = new uint[]
			{
				Parameter.Interfaces.Idx.interfacesindex_2001,
				Parameter.Interfaces.Idx.interfacescustomdescription_2017,
			};
			var columns = protocol.GetColumns(Parameter.Interfaces.tablePid, columnIndexes);
			var primaryKeys = (object[])columns[0];
			var customDescriptions = (object[])columns[1];

			var customDescriptionsByKey = primaryKeys
				.Zip(customDescriptions, (primaryKey, value) => new { k = primaryKey, v = value })
				.ToDictionary(x => (string)x.k, x => (string)x.v);

			return customDescriptionsByKey
				.Where(pair => string.IsNullOrWhiteSpace(pair.Value))
				.ToDictionary(pair => pair.Key, pair => "-1");
		}
	}
}