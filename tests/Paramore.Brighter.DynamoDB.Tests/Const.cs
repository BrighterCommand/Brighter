using System;
using Amazon.DynamoDBv2;
using Amazon.Runtime;

namespace Paramore.Brighter.DynamoDB.Tests;

public static class Const
{
    // Integration tests allow for emulator latency; deadline semantics use a fake clock separately.
    public const int OUTBOX_TIMEOUT_IN_MILLISECONDS = 10_000;

    static Const()
    {
        Environment.SetEnvironmentVariable("AWS_ENABLE_ENDPOINT_DISCOVERY", "false");

        DynamoDbClient  = new AmazonDynamoDBClient(
            new BasicAWSCredentials("FakeAccessKey", "FakeSecretKey"),
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" });
    }
    
    public static AmazonDynamoDBClient DynamoDbClient { get; }
}
