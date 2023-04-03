namespace Paramore.Brighter.MsSql
{
    public class MsSqlConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlConfiguration"/> class.
        /// </summary>
        /// <param name="connectionString">The connection string. Please note the latest library defaults Encryption to on
        /// if this is a issue add 'Encrypt=false' to your connection string.</param>
        /// <param name="outBoxTableName">Name of the outbox table.</param>
        /// <param name="inboxTableName">Name of the inbox table.</param>
        /// <param name="queueStoreTable">Name of the queue store table.</param>
        public MsSqlConfiguration(string connectionString, string outBoxTableName = null, string inboxTableName = null, string queueStoreTable = null)
        {
            OutBoxTableName = outBoxTableName;
            ConnectionString = connectionString;
            InBoxTableName = inboxTableName;
            QueueStoreTable = queueStoreTable;
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
    }
}
