using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb.Extensions
{
    public class DynamoDbCreateProvisionedThroughput
    {
        public ProvisionedThroughput Table { get; }
        public Dictionary<string, ProvisionedThroughput> GSIThroughputs { get; }

        public DynamoDbCreateProvisionedThroughput(
            ProvisionedThroughput table = null,
            Dictionary<string, ProvisionedThroughput> gsiThroughputs = null)
        {
            //TODO: Sensible default value for table throughput?
            Table = table ?? new ProvisionedThroughput(readCapacityUnits: 100, writeCapacityUnits: 100);
            GSIThroughputs = gsiThroughputs;
        }
    }
}
