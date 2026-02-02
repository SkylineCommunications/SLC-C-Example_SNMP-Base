namespace QAction_1991
{
    using System.Collections.Generic;
    using System.Linq;

    using Skyline.DataMiner.Scripting;
    using Skyline.DataMiner.Utils.Protocol.Extension;

    internal class InterfacesTableGetter
	{
		private readonly SLProtocol protocol;

		public InterfacesTableGetter(SLProtocol protocol)
		{
			this.protocol = protocol;
		}

		public object[] Keys { get; set; }

		public object[] CustomDescriptions { get; set; }

		public Dictionary<string, string> CustomDescriptionsByKey { get; set; }

		public void Load()
		{
			var columnIndexes = new uint[]
			{
				Parameter.Interfaces.Idx.interfacesindex,
				Parameter.Interfaces.Idx.interfacescustomdescription,
			};

			var columns = protocol.GetColumns(Parameter.Interfaces.tablePid, columnIndexes);

			Keys = (object[])columns[0];
			CustomDescriptions = (object[])columns[1];

			CustomDescriptionsByKey = Keys
				.Zip(CustomDescriptions, (key, value) => new { k = key, v = value })
				.ToDictionary(x => (string)x.k, x => (string)x.v);
		}
	}
}
