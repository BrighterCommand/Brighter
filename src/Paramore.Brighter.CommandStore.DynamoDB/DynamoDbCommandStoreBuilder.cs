using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.CommandStore.DynamoDB
{
    public class DynamoDbCommandStoreBuilder
    {
        private readonly string _tableName = "brighter_command_store";

        public DynamoDbCommandStoreBuilder() { }

        public DynamoDbCommandStoreBuilder(string tableName)
        {
            _tableName = tableName;
        }

        public CreateTableRequest CreateCommandStoreTableRequest(int readCapacityUnits = 2, int writeCapacityUnits = 1)
        {
            var provisionedThroughput = new ProvisionedThroughput
            {
                ReadCapacityUnits = readCapacityUnits,
                WriteCapacityUnits = writeCapacityUnits
            };

            return new CreateTableRequest
            {
                TableName = _tableName,
                ProvisionedThroughput = provisionedThroughput,
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "Key",
                        AttributeType = ScalarAttributeType.S
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "TimeStamp",
                        AttributeType = ScalarAttributeType.S
                    }
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = "Key",
                        KeyType = KeyType.HASH
                    },
                    new KeySchemaElement
                    {
                        AttributeName = "TimeStamp",
                        KeyType = KeyType.RANGE
                    }
                },
                GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                {
                    new GlobalSecondaryIndex
                    {
                        IndexName = "MessageId",
                        ProvisionedThroughput = provisionedThroughput,
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement
                            {
                                AttributeName = "MessageId",
                                KeyType = KeyType.HASH
                            }
                        },
                        Projection = new Projection
                        {
                            ProjectionType = ProjectionType.ALL
                        }
                    }
                }
            };
        }
    }
}
