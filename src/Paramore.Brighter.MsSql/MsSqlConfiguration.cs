namespace Paramore.Brighter.MsSql
{
    public class MsSqlConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlConfiguration"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="outBoxTableName">Name of the outbox table.</param>
        /// <param name="inboxTableName">Name of the inbox table.</param>
        /// <param name="queueStoreTable">Name of the queue store table.</param>
        /// <param name="encrypt">Gets the name of the queue table.</param>
        public MsSqlConfiguration(string connectionString, string outBoxTableName = null, string inboxTableName = null, string queueStoreTable = null, bool encrypt = false)
        {
            OutBoxTableName = outBoxTableName;
            ConnectionString = connectionString;
            InBoxTableName = inboxTableName;
            QueueStoreTable = queueStoreTable;
            Encrypt = encrypt;
        }

        /// <summary>
        /// Gets the connection string.
        /// </summary>
        /// <value>The connection string.</value>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Gets the name of the outbox table.
        /// </summary>
        /// <value>The name of the outbox table.</value>
        public string OutBoxTableName { get; private set; }
        
        /// <summary>
        /// Gets the name of the inbox table.
        /// </summary>
        /// <value>The name of the inbox table.</value>
        public string InBoxTableName { get; private set; }
        
        /// <summary>
        /// Gets the name of the queue table.
        /// </summary>
        public string QueueStoreTable { get; private set; }

        /// <summary>
        /// Use an encrypted Sql connection.
        /// </summary>
        public bool Encrypt { get; private set; }
    }
}
