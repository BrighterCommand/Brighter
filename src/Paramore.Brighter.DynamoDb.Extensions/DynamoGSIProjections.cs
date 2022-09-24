using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoGSIProjections
    {
        public Dictionary<string, Projection> Projections { get; set; }

        public DynamoGSIProjections(Dictionary<string, Projection> projections)
        {
            Projections = projections;
        }
    }
}
