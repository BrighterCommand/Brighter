using System;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoDbConfiguration
    {
        /// <summary>
        /// The table that forms the Outbox
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The name of the global secondary delivered index indexed by topic
        /// </summary>
        public string DeliveredIndexName { get; set; }

        /// <summary>
        /// The name of the global secondary delivered index convering all topics
        /// </summary>
        public string DeliveredAllTopicsIndexName { get; set; }

        /// <summary>
        /// The name of the global secondary outstanding index indexed by topic
        /// </summary>
        public string OutstandingIndexName { get; set; }

        /// <summary>
        /// The name of the global secondary outstanding index convering all topics
        /// </summary>
        public string OutstandingAllTopicsIndexName { get; set; }
        
        /// <summary>
        /// Timeout in milliseconds
        /// </summary>
        public int Timeout { get; }
        
        /// <summary>
        /// Number of shards to use for the outstanding index. Maximum of 20
        /// </summary>
        public int NumberOfShards { get; }
        
        /// <summary>
        /// Optional time to live for the messages in the outbox
        /// By default, messages will not expire
        /// </summary>
        public TimeSpan? TimeToLive { get; set; }

        /// <summary>
        /// The number of concurrent scans to use in a parallel scan when looking for outstanding messages
        /// </summary>
        public int ScanConcurrency { get; set; }

        /// <summary>
        /// Create a DynamoDbConfiguration for Outbox support
        /// </summary>
        /// <param name="tableName">The name of the outbox table</param>
        /// <param name="timeout">The timeout when talking to DynamoDb</param>
        /// <param name="numberOfShards">The number of shards; use more than one shard for active topics to avoid hotspots</param>
        public DynamoDbConfiguration(string? tableName = null, int timeout = 500, int numberOfShards = 3, int scanConcurrency = 3)
        {
            TableName = tableName ?? "brighter_outbox";
            OutstandingIndexName = "Outstanding";
            OutstandingAllTopicsIndexName = "OutstandingAllTopics";
            DeliveredIndexName = "Delivered";
            DeliveredAllTopicsIndexName = "DeliveredAllTopics";
            Timeout = timeout;
            NumberOfShards = numberOfShards;
            ScanConcurrency = scanConcurrency;
        }
    }
}
