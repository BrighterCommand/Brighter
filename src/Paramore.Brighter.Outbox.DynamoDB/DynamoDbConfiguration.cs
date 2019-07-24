using Amazon;
using Amazon.Runtime;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoDbConfiguration
    {
        //What AWS Credentials to use
        public AWSCredentials Credentials { get; }
        /// <summary>
        /// Which AWS region
        /// </summary>
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
        /// Timeout in milliseconds
        /// </summary>
        public int Timeout { get; }

        public DynamoDbConfiguration(
            AWSCredentials credentials, 
            RegionEndpoint region,
            string tableName = null,
            int timeout = 500)
        {
            Credentials = credentials;
            Region = region;
            TableName = tableName ?? "brighter_outbox";
            OutstandingIndexName = "Outstanding";
            DeliveredIndexName = "Delivered";
            Timeout = timeout;
        }
    }
}
