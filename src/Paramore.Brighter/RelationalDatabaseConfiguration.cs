using System;

namespace Paramore.Brighter
{
    public class RelationalDatabaseConfiguration : IAmARelationalDatabaseConfiguration
    {
        private const string DATABASE_NAME = "Brighter";
        private const string OUTBOX_TABLE_NAME = "Outbox";
        private const string INBOX_TABLE_NAME = "Inbox";
        private const string QUEUE_TABLE_NAME = "Queue";

        // Default command timeout in seconds for the outstanding message count, used when OutstandingCountTimeout is not set.
        internal const int OUTSTANDING_COUNT_TIMEOUT_SECONDS = 15;
        private const int MAX_OUTSTANDING_COUNT_TIMEOUT_SECONDS = int.MaxValue / 1000;

        /// <summary>
        /// Initializes a new instance of the <see cref="RelationalDatabaseConfiguration"/> class. 
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="databaseName">The database name.</param>
        /// <param name="outBoxTableName">Name of the outbox table.</param>
        /// <param name="inboxTableName">Name of the inbox table.</param>
        /// <param name="queueStoreTable">Name of the queue store table.</param>
        /// <param name="schemaName">The schema name.</param>
        /// <param name="binaryMessagePayload">Is the message payload binary, or a UTF-8 string, default is false or UTF-8</param>
        /// <param name="jsonMessagePayload">Whether to persist the message payload using the database’s native JSON type</param>
        /// <param name="outstandingCountTimeout">The command timeout for the outbox's outstanding message count, rounded up to whole seconds, default is null or 15 seconds</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="outstandingCountTimeout"/> is not greater than zero or is more than 2147483 seconds.</exception>
        public RelationalDatabaseConfiguration(
            string connectionString,
            string? databaseName = null,
            string? outBoxTableName = null,
            string? inboxTableName = null,
            string? queueStoreTable = null,
            string? schemaName = null,
            bool binaryMessagePayload = false,
            bool jsonMessagePayload = false,
            TimeSpan? outstandingCountTimeout = null
        )
        {
            _ = ToOutstandingCountTimeoutSeconds(outstandingCountTimeout, nameof(outstandingCountTimeout));

            OutstandingCountTimeout = outstandingCountTimeout;
            DatabaseName = databaseName ?? DATABASE_NAME;
            OutBoxTableName = outBoxTableName ?? OUTBOX_TABLE_NAME;
            InBoxTableName = inboxTableName ?? INBOX_TABLE_NAME;
            ConnectionString = connectionString;
            QueueStoreTable = queueStoreTable ?? QUEUE_TABLE_NAME;
            SchemaName = schemaName;
            BinaryMessagePayload = binaryMessagePayload;
            JsonMessagePayload = jsonMessagePayload;
        }

        /// <summary>
        /// Is the message payload binary, or a UTF-8 string. Default is false or UTF-8
        /// </summary>
        public bool BinaryMessagePayload { get; protected set; }

        /// <summary>
        /// Whether to persist the message payload using the database’s native JSON type
        /// </summary>
        public bool JsonMessagePayload { get; protected set; }

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        /// <value>The connection string.</value>
        public string ConnectionString { get; protected set; }

        /// <summary>
        /// Gets the name of the database containing the tables.
        /// </summary>
        /// <value>The name of the database.</value>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the name of the inbox table.
        /// </summary>
        /// <value>The name of the inbox table.</value>
        public string InBoxTableName { get; private set; }

        /// <summary>
        /// Gets the name of the outbox table.
        /// </summary>
        /// <value>The name of the outbox table.</value>
        public string OutBoxTableName { get; protected set; }

        /// <summary>
        /// Gets the name of the queue table.
        /// </summary>
        public string QueueStoreTable { get; protected set; }

        /// <summary>
        /// Gets the command timeout for the outbox's outstanding message count.
        /// </summary>
        /// <value>The timeout, or <c>null</c> for the default of 15 seconds.</value>
        /// <remarks>Only the count uses it, rounded up to whole seconds; reading the outstanding messages is not bounded by it.</remarks>
        public TimeSpan? OutstandingCountTimeout { get; }

        /// <summary>
        /// Get the name of the Schema
        /// </summary>
        public string? SchemaName { get; protected set; }

        // Validates an outstanding count timeout and converts it to the whole seconds a command timeout takes,
        // so the outbox can also check a configuration that did not come through this constructor.
        internal static int ToOutstandingCountTimeoutSeconds(TimeSpan? outstandingCountTimeout, string paramName)
        {
            if (outstandingCountTimeout <= TimeSpan.Zero
                || outstandingCountTimeout > TimeSpan.FromSeconds(MAX_OUTSTANDING_COUNT_TIMEOUT_SECONDS))
                throw new ArgumentOutOfRangeException(paramName, outstandingCountTimeout,
                    $"The outstanding count timeout must be greater than zero and no more than {MAX_OUTSTANDING_COUNT_TIMEOUT_SECONDS} seconds.");

            return (int)Math.Ceiling((outstandingCountTimeout ?? TimeSpan.FromSeconds(OUTSTANDING_COUNT_TIMEOUT_SECONDS)).TotalSeconds);
        }
    }
}
