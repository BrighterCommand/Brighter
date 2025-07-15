using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb.V4;

public class DynamoGSIProjections(Dictionary<string, Projection> projections)
{
    public Dictionary<string, Projection> Projections { get; set; } = projections;
}
