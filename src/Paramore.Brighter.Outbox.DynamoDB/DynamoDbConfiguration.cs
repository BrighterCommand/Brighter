using System;
using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoDbConfiguration
    {   
        /// <summary>
        /// What AWS Credentials to use
        /// </summary>
        [Obsolete("This property is not being used")]
        public AWSCredentials Credentials { get; }
        /// <summary>
        /// Which AWS region
        /// </summary>
        [Obsolete("This property is not being used")]
        public RegionEndpoint Region { get; }
        /// <summary>
        /// The table that forms the Outbox
        /// </summary>
        public string TableName { get; set; }
        /// <summary>
        /// The name of local delivered status index
        /// </summary>
        public string DeliveredIndexName { get; }
        /// <summary>
        /// The name of the global secondary outstanding index
        /// </summary>
        public string OutstandingIndexName { get; set; }
        /// <summary>
        /// Whether the outbox table uses a sparse outstanding index
        /// </summary>
        public bool SparseOutstandingIndex { get; set; }
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
    
        [Obsolete("Use the DynamoDbConfiguration without AWSCredentials and without RegionEndpoint")]
        public DynamoDbConfiguration(
            AWSCredentials credentials, 
            RegionEndpoint region,
            string tableName = null,
            int timeout = 500,
            int numberOfShards = 3,
            bool sparseOutstandingIndex = false)
        {
            Credentials = credentials;
            Region = region;
            TableName = tableName ?? "brighter_outbox";
            OutstandingIndexName = "Outstanding";
            DeliveredIndexName = "Delivered";
            Timeout = timeout;
            NumberOfShards = numberOfShards;
            SparseOutstandingIndex = sparseOutstandingIndex;
        }

        public DynamoDbConfiguration(string tableName = null, int timeout = 500, int numberOfShards = 3, bool sparseOutstandingIndex = false)
        {
            TableName = tableName ?? "brighter_outbox";
            OutstandingIndexName = "Outstanding";
            DeliveredIndexName = "Delivered";
            Timeout = timeout;
            NumberOfShards = numberOfShards;
            SparseOutstandingIndex = sparseOutstandingIndex;
        }
    }
}
