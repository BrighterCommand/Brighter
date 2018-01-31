using System;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    /// <summary>
    ///     Class MsSqlMessagingGatewayConfiguration
    /// </summary>
    public class MsSqlMessagingGatewayConfiguration
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MsSqlMessagingGatewayConfiguration"/>
        /// </summary>
        /// <param name="connectionString">The connectionstring to the database</param>
        /// <param name="queueStoreTable">The table name to use for queue data</param>
        public MsSqlMessagingGatewayConfiguration(string connectionString, string queueStoreTable)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            QueueStoreTable = queueStoreTable ?? throw new ArgumentNullException(nameof(queueStoreTable));
        }

        public string ConnectionString { get; }
        public string QueueStoreTable { get; }
    }
}