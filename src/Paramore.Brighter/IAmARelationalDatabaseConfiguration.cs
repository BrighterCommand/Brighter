﻿namespace Paramore.Brighter
{
    public interface IAmARelationalDatabaseConfiguration
    {
        /// <summary>
        /// Is the message payload binary, or a UTF-8 string. Default is false or UTF-8
        /// </summary>
        bool BinaryMessagePayload { get; }

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        /// <value>The connection string.</value>
        string ConnectionString { get; }

        /// <summary>
        /// Gets the name of the inbox table.
        /// </summary>
        /// <value>The name of the inbox table.</value>
        string InBoxTableName { get; }

        /// <summary>
        /// Gets the name of the outbox table.
        /// </summary>
        /// <value>The name of the outbox table.</value>
        string OutBoxTableName { get; }

        /// <summary>
        /// Gets the name of the queue table.
        /// </summary>
        string QueueStoreTable { get; }
    }
}
