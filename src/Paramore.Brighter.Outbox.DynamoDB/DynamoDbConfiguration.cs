using Amazon;
using Amazon.DynamoDBv2.Model;
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
        /// The name of the global secondary message index
        /// </summary>
        public string MessageIdIndexName { get; set; }
        /// <summary>
        /// Use strongly consistent reads; has latency implications
        /// </summary>
        public bool UseStronglyConsistentRead { get; }
        /// <summary>
        /// Timeout in milliseconds
        /// </summary>
        public int Timeout { get; }
        /// <summary>
        /// What is the throughput of the table
        /// </summary>
        public ProvisionedThroughput TableProvisionedThroughput { get; }
        /// <summary>
        /// Provionsed Throughput for the MessgeId Global Index
        /// </summary>
        public ProvisionedThroughput MessageIdIndexThroughput { get; }
        /// <summary>
        /// Provisioned throughput for the Delivered Global Index
        /// </summary>
        public ProvisionedThroughput DeliveredIndexThroughput { get; }


        public DynamoDbConfiguration(
            AWSCredentials credentials, 
            RegionEndpoint region,
            string tableName = null, 
            ProvisionedThroughput tableProvisionedThroughput = null,
            ProvisionedThroughput messageIdThroughput = null,
            ProvisionedThroughput deliveredIndexThroughput = null,
            bool useStronglyConsistentRead = true, 
            int timeout = 500)
        {
            Credentials = credentials;
            Region = region;
            TableName = tableName ?? "brighter_message_store";
            MessageIdIndexName = "MessageId";
            DeliveredIndexName = "Delivered";
            //TODO: Is this a sensible default?
            TableProvisionedThroughput = tableProvisionedThroughput ?? new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 5};
            MessageIdIndexThroughput = messageIdThroughput ?? new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 5};
            DeliveredIndexThroughput = deliveredIndexThroughput ?? new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 5};
            UseStronglyConsistentRead = useStronglyConsistentRead;
            Timeout = timeout;
        }
    }
}
