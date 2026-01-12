namespace Skyline.Protocol.Api.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class PrimaryKeyNotFoundException : Exception
    {
        public PrimaryKeyNotFoundException()
        {
        }

        public PrimaryKeyNotFoundException(string message)
            : base(message)
        {
        }

        public PrimaryKeyNotFoundException(int tableId, string primaryKey)
            : this(BuildMessage(tableId, primaryKey))
        {
        }

        public PrimaryKeyNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public PrimaryKeyNotFoundException(int tableId, string primaryKey, Exception innerException)
            : this(BuildMessage(tableId, primaryKey), innerException)
        {
        }

        protected PrimaryKeyNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        private static string BuildMessage(int tableId, string primaryKey)
        {
            return $"Table<{tableId}>: Primary key with value '{primaryKey}' not found.";
        }
    }
}