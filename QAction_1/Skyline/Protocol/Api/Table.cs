// Ignore Spelling: Api

namespace Skyline.Protocol.Api
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Skyline.DataMiner.Scripting;
    using Skyline.Protocol.Api.Exceptions;
    using SLNetMessages = Skyline.DataMiner.Net.Messages;

    /// <summary>
    /// Represents a table.
    /// </summary>
    internal static class Table
    {
        /// <summary>
        /// Determines whether a row with the specified key exists in the specified table.
        /// </summary>
        /// <param name="protocol">Link with SLProtocol process.</param>
        /// <param name="tableId">The ID of the table.</param>
        /// <param name="key">The primary key of the row.</param>
        /// <returns><see langword="true"/> when a row with the key is exists; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
        internal static bool Exists(SLProtocol protocol, int tableId, string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return protocol.Exists(tableId, key);
        }

        /// <summary>
        /// Adds or sets a row to the specified table.
        /// </summary>
        /// <param name="protocol">Link with SLProtocol process.</param>
        /// <param name="tableId">The ID of the table.</param>
        /// <param name="row">The row to add or set.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="row"/> is <see langword="null"/>.</exception>
        /// <inheritdoc cref="Exists(SLProtocol, int, string)"/>
        /// <inheritdoc cref="AddRow(SLProtocol, int, QActionTableRow)"/>
        /// <inheritdoc cref="SetRow(SLProtocol, int, QActionTableRow)"/>
        internal static void AddOrSetRow(SLProtocol protocol, int tableId, QActionTableRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (Exists(protocol, tableId, row.Key))
            {
                SetRow(protocol, tableId, row);
                return;
            }

            AddRow(protocol, tableId, row);
        }

        /// <summary>
        /// Removes the specified row from the specified table.
        /// </summary>
        /// <param name="protocol">Link with SLProtocol process.</param>
        /// <param name="tableId">The ID of the table.</param>
        /// <param name="key">The primary key of the row to remove.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
        internal static void DeleteRow(SLProtocol protocol, int tableId, string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            protocol.DeleteRow(tableId, key);
        }

        /// <summary>
        /// Removes the specified row(s) from the specified table.
        /// </summary>
        /// <param name="protocol">Link with SLProtocol process.</param>
        /// <param name="tableId">The ID of the table.</param>
        /// <param name="keys">The primary keys of the rows to remove.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="keys"/> is <see langword="null"/>.</exception>
        internal static void DeleteRows(SLProtocol protocol, int tableId, IEnumerable<string> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            if (!keys.Any())
            {
                return;
            }

            protocol.DeleteRow(tableId, keys.ToArray());
        }

        internal static object[] GetRow(SLProtocol protocol, int tableId, string primaryKey)
        {
            if (!Exists(protocol, tableId, primaryKey))
            {
                throw new PrimaryKeyNotFoundException(tableId, primaryKey);
            }

            return (object[])protocol.GetRow(tableId, primaryKey);
        }

        /// <summary>
        /// Adds or updates the provided rows to the specified table.
        /// </summary>
        /// <param name="protocol">Link with SLProtocol process.</param>
        /// <param name="tableId">The ID of the table.</param>
        /// <param name="rows">The rows to sets.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is <see langword="null"/>.</exception>
        /// <inheritdoc cref="FillArray(SLProtocol, int, NotifyProtocol.SaveOption, IEnumerable{QActionTableRow})"/>
        internal static void FillArrayNoDelete(SLProtocol protocol, int tableId, IEnumerable<QActionTableRow> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (!rows.Any())
            {
                return;
            }

            FillArray(protocol, tableId, NotifyProtocol.SaveOption.Partial, rows);
        }

        /// <summary>
        /// Adds a row to the specified table.
        /// </summary>
        /// <param name="protocol">Link with SLProtocol process.</param>
        /// <param name="tableId">The ID of the table.</param>
        /// <param name="row">The row to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="row"/> is <see langword="null"/>.</exception>
        internal static void AddRow(SLProtocol protocol, int tableId, QActionTableRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            protocol.AddRow(tableId, row.ToObjectArray());
        }

        internal static void AddRow(SLProtocol protocol, int tableId, QActionTableRow row, DateTime timestamp)
        {
            protocol.AddRow(tableId, new object[] { row.ToObjectArray(), timestamp });
        }

        internal static void SetRow(SLProtocol protocol, int tableId, QActionTableRow row, DateTime dateTime)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (!Exists(protocol, tableId, row.Key))
            {
                throw new PrimaryKeyNotFoundException(tableId, row.Key);
            }

            protocol.SetRow(tableId, row.Key, row.ToObjectArray(), dateTime);
        }

        internal static void ClearAllKeys(SLProtocol protocol, int tableId)
        {
            protocol.ClearAllKeys(tableId);
        }

        internal static void FillArray(SLProtocol protocol, int tableId, NotifyProtocol.SaveOption saveOption, IEnumerable<QActionTableRow> rows)
        {
            var rowCollection = rows.Select(row => row.ToObjectArray()).ToList();
            protocol.FillArray(tableId, rowCollection, saveOption);
        }

        /// <summary>
        /// Sets a row of the specified table.
        /// </summary>
        /// <param name="protocol">Link with SLProtocol process.</param>
        /// <param name="tableId">The ID of the table.</param>
        /// <param name="row">The row to set.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="row"/> is <see langword="null"/>.</exception>
        private static void SetRow(SLProtocol protocol, int tableId, QActionTableRow row)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            protocol.SetRow(tableId, row.Key, row.ToObjectArray());
        }

        /// <summary>
        /// Represents a column.
        /// </summary>
        internal static class Column
        {
            /// <summary>
            /// Gets the value for the specified cell.
            /// </summary>
            /// <param name="protocol">Link with SLProtocol process.</param>
            /// <param name="tableId">The ID of the table.</param>
            /// <param name="key">The row key.</param>
            /// <param name="index">The 0-based column index.</param>
            /// <returns>The cell value.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is <see langword="null"/>.</exception>
            internal static object GetValue(SLProtocol protocol, int tableId, string key, int index)
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                return protocol.GetParameterIndexByKey(tableId, key, index + 1);
            }

            /// <summary>
            /// Gets the values from the specified table's 0-based column.
            /// </summary>
            /// <param name="protocol">Link with SLProtocol process.</param>
            /// <param name="tableId">The ID of the table.</param>
            /// <param name="index">The 0-based column index.</param>
            /// <returns>A collection of values.</returns>
            internal static object[] GetValues(SLProtocol protocol, int tableId, uint index)
            {
                uint[] indexes = new[]
                {
                    index,
                };
                var columns = (object[])protocol.NotifyProtocol((int)SLNetMessages.NotifyType.NT_GET_TABLE_COLUMNS, tableId, indexes);
                var values = (object[])columns[0];

                return values;
            }

            /// <summary>
            /// Determines whether the row with the specified key exists and returns value for the specified column accordingly.
            /// </summary>
            /// <param name="protocol">Link with SLProtocol process.</param>
            /// <param name="tableId">The ID of the table.</param>
            /// <param name="key">The row key.</param>
            /// <param name="index">The 0-based column index.</param>
            /// <param name="value">The retrieved value.</param>
            /// <returns><see langword="true"/> if the cell value could be retrieved;otherwise, <see langword="false"/>.</returns>
            /// <inheritdoc cref="Exists(SLProtocol, int, string)"/>
            /// <inheritdoc cref="GetValue(SLProtocol, int, string, int)"/>
            internal static bool TryGetValue(SLProtocol protocol, int tableId, string key, int index, out object value)
            {
                value = Exists(protocol, tableId, key) ?
                    GetValue(protocol, tableId, key, index) :
                    null;

                return value != null;
            }

            internal static Dictionary<string, object> GetColumnValuesByKey(SLProtocol protocol, int tableId, int indexColumn, int columnIndex)
            {
                return GetColumnValuesByKey(protocol, tableId, (uint)indexColumn, (uint)columnIndex);
            }

            /// <summary>
            /// Sets the value of a cell in a table, identified by the primary key of the row and the 0-based column index to the specified value.
            /// </summary>
            /// <param name="protocol">Link with SLProtocol process.</param>
            /// <param name="tableId">The ID of the table.</param>
            /// <param name="key">The row to set.</param>
            /// <param name="index">The 0-based column index.</param>
            /// <param name="value">The value to be set.</param>
            /// <exception cref="KeyNotFoundException">Thrown when <paramref name="key"/> doesn't exist.</exception>
            internal static void SetValue(SLProtocol protocol, int tableId, string key, int index, object value)
            {
                if (!Exists(protocol, tableId, key))
                {
                    throw new KeyNotFoundException($"Table<{tableId}>: Primary key with value '{key}' doesn't exist.");
                }

                protocol.SetParameterIndexByKey(tableId, key, index + 1, value);
            }

            internal static void SetValues(SLProtocol protocol, int tableId, int columnId, IEnumerable<string> keys, IEnumerable<object> values)
            {
                if (keys == null)
                {
                    throw new ArgumentNullException(nameof(keys));
                }

                if (values == null)
                {
                    throw new ArgumentNullException(nameof(values));
                }

                if (keys.Count() != values.Count())
                {
                    throw new InvalidOperationException();
                }

                if (!keys.Any() || !values.Any())
                {
                    return;
                }

                object[] columnInfo = new object[]
                {
                    tableId,
                    columnId,
                };
                object[] columnValues = new object[]
                {
                    new List<object>(keys).ToArray(),
                    new List<object>(values).ToArray(),
                };
                protocol.NotifyProtocol((int)SLNetMessages.NotifyType.NT_FILL_ARRAY_WITH_COLUMN, columnInfo, columnValues);
            }

            private static Dictionary<string, object> GetColumnValuesByKey(SLProtocol protocol, int tableId, uint indexColumn, uint columnIndex)
            {
                uint[] columnIndexes = new[]
                {
                    indexColumn,
                    columnIndex,
                };
                object[] columns = (object[])protocol.NotifyProtocol((int)SLNetMessages.NotifyType.NT_GET_TABLE_COLUMNS, tableId, columnIndexes);
                object[] primaryKeys = (object[])columns[0];
                object[] values = (object[])columns[1];

                return primaryKeys.Zip(values, (primaryKey, value) => new { k = primaryKey, v = value }).ToDictionary(x => (string)x.k, x => x.v);
            }
        }
    }
}