namespace Skyline.Protocol.Api.Helpers
{
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Scripting;

    public static class InterfacesTableHelper
    {
        /// <summary>
        /// Sets exception values to empty cells in custom description column.
        /// </summary>
        /// <param name="protocol">Link to SLProtocol process.</param>
        public static void InitialiseCustomDescriptionColumn(SLProtocol protocol)
        {
            var pairsToSetException = BuildCustomDescriptionPairsToSetException(protocol);
            InterfacesTable.CustomDescriptionColumn.SetValuesByKey(protocol, pairsToSetException);
        }

        private static Dictionary<string, string> BuildCustomDescriptionPairsToSetException(SLProtocol protocol)
        {
            var customDescriptionsByKey = InterfacesTable.CustomDescriptionColumn.GetValuesByKey(protocol);

            return customDescriptionsByKey
                .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key, pair => InterfacesTable.CustomDescriptionColumn.Exception);
        }
    }
}