namespace Skyline.Protocol.Interfaces
{
    using System;
    using System.Collections.Generic;
    using Skyline.DataMiner.Scripting;
    using Skyline.DataMiner.Utils.Interfaces;
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

        public Dictionary<string, DuplexStatus> DuplexStatusesByKey { get; private set; }

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

            DuplexStatusesByKey = ConvertDuplexColumnToDictionary();
        }

        private Dictionary<string, DuplexStatus> ConvertDuplexColumnToDictionary()
        {
            var duplexStatuses = new Dictionary<string, DuplexStatus>();
            for (int i = 0; i < Keys.Length; i++)
            {
                string key = Convert.ToString(Keys[i]);
                var duplexStatus = (DuplexStatus)Convert.ToInt32(DuplexStatuses[i]);

                duplexStatuses[key] = duplexStatus;
            }

            return duplexStatuses;
        }
    }
}