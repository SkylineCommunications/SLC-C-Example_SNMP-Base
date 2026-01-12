namespace Skyline.Protocol.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Scripting;

    public static class InterfacesTable
    {
        private const int TableId = Parameter.Interfaces.tablePid;
        private const int IndexCol = Parameter.Interfaces.indexColumn;

        public static void FillArray(SLProtocol protocol, IEnumerable<InterfacesQActionRow> rows)
        {
            Table.FillArray(protocol, TableId, NotifyProtocol.SaveOption.Full, rows);
        }

        public static class CustomDescriptionColumn
        {
            public static readonly string Exception = "-1";

            private const int ColId = Parameter.Interfaces.Pid.interfacescustomdescription_2018;
            private const int ColIdx = Parameter.Interfaces.Idx.interfacescustomdescription_2018;

            public static Dictionary<string, string> GetValuesByKey(SLProtocol protocol)
            {
                return Table.Column
                    .GetColumnValuesByKey(protocol, TableId, IndexCol, ColIdx)
                    .ToDictionary(pair => pair.Key, pair => Convert.ToString(pair.Value));
            }

            public static void SetValuesByKey(SLProtocol protocol, Dictionary<string, string> valuesToSet)
            {
                Table.Column.SetValues(protocol, TableId, ColId, valuesToSet.Keys, valuesToSet.Values.Select(val => (object)val));
            }
        }
    }
}