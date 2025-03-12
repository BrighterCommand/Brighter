namespace Paramore.Brighter
{
    public class RelationalDatabaseConfiguration : IAmARelationalDatabaseConfiguration
    {
        private const string DATABASE_NAME = "Brighter";
        private const string OUTBOX_TABLE_NAME = "Outbox";
        private const string INBOX_TABLE_NAME = "Inbox";
        private const string QUEUE_TABLE_NAME = "Queue";

        /// <summary>
        /// Initializes a new instance of the <see cref="RelationalDatabaseConfiguration"/> class. 
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="outBoxTableName">Name of the outbox table.</param>
        /// <param name="inboxTableName">Name of the inbox table.</param>
        /// <param name="queueStoreTable">Name of the queue store table.</param>
        /// <param name="binaryMessagePayload">Is the message payload binary, or a UTF-8 string, default is false or UTF-8</param>
        public RelationalDatabaseConfiguration(
            string connectionString,
            string? databaseName = null,
            string? outBoxTableName = null,
            string? inboxTableName = null,
            string? queueStoreTable = null,
            bool binaryMessagePayload = false
        )
        {
            DatabaseName = databaseName ?? DATABASE_NAME;
            OutBoxTableName = outBoxTableName ?? OUTBOX_TABLE_NAME;
            InBoxTableName = inboxTableName ?? INBOX_TABLE_NAME;
            ConnectionString = connectionString;
            QueueStoreTable = queueStoreTable ?? QUEUE_TABLE_NAME;
            BinaryMessagePayload = binaryMessagePayload;
        }

        /// <summary>
        /// Is the message payload binary, or a UTF-8 string. Default is false or UTF-8
        /// </summary>
        public bool BinaryMessagePayload { get; protected set; }

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
    }
}
