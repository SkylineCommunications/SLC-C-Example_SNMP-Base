namespace Skyline.Protocol.Interfaces
{
    using System;
    using System.Collections.Generic;
    using Skyline.DataMiner.Scripting;
    using Skyline.DataMiner.Utils.Protocol.Extension;

    public sealed class DuplexGetter
    {
        private readonly SLProtocol protocol;

        public DuplexGetter(SLProtocol protocol)
        {
            this.protocol = protocol;
        }

        public object[] Keys { get; private set; }

        public object[] DuplexStatuses { get; private set; }

        public static Dictionary<string, int> GetDuplexStatusesByKey(SLProtocol protocol)
        {
            var duplexStatusesPerKey = new Dictionary<string, int>();

            var columnsToGet = new uint[]
            {
            Parameter.Dot3stats.Idx.dot3stats_index,
            Parameter.Dot3stats.Idx.dot3stats_duplexstatus,
            };

            var columns = protocol.GetColumns(Parameter.Dot3stats.tablePid, columnsToGet);
            var keys = (object[])columns[0];
            var duplexStatuses = (object[])columns[1];

            for (var i = 0; i < keys.Length; i++)
            {
                duplexStatusesPerKey[Convert.ToString(keys[i])] = Convert.ToInt32(duplexStatuses[i]);
            }

            return duplexStatusesPerKey;
        }

        public void Load()
        {
            var columnsToGet = new uint[]
            {
                    Parameter.Dot3stats.Idx.dot3stats_index,
                    Parameter.Dot3stats.Idx.dot3stats_duplexstatus,
            };

            var tableData = protocol.GetColumns(Parameter.Dot3stats.tablePid, columnsToGet);

            Keys = (object[])tableData[0];
            DuplexStatuses = (object[])tableData[1];
        }
    }
}