using System.Collections.Generic;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb.V4;

public class DynamoGSIProjections
{
    public Dictionary<string, Projection> Projections { get; set; }

    public DynamoGSIProjections(Dictionary<string, Projection> projections)
    {
        Projections = projections;
    }
}