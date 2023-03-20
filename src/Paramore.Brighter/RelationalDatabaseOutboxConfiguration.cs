namespace Paramore.Brighter
{
    public class RelationalDatabaseOutboxConfiguration
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="RelationalDatabaseOutboxConfiguration"/> class. 
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="outBoxTableName">Name of the outbox table.</param>
        /// <param name="inboxTableName">Name of the inbox table.</param>
        /// <param name="queueStoreTable">Name of the queue store table.</param>
        /// <param name="binaryMessagePayload">Is the message payload binary, or a UTF-8 string, default is false or UTF-8</param>
        protected RelationalDatabaseOutboxConfiguration(
            string connectionString,
            string outBoxTableName = null,
            string queueStoreTable = null,
            bool binaryMessagePayload = false
        )
        {
            OutBoxTableName = outBoxTableName;
            ConnectionString = connectionString;
            QueueStoreTable = queueStoreTable;
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
