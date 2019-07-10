using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoDbOutboxBuilder
    {
        private readonly string _tableName = "brighter_message_store";

        public CreateTableRequest CreateOutboxTableRequest(DynamoDbConfiguration dbConfiguration)
        {
            return new CreateTableRequest
            {
                TableName = _tableName,
                ProvisionedThroughput = dbConfiguration.TableProvisionedThroughput,
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "Topic+Date",
                        AttributeType = ScalarAttributeType.S
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "CreatedTime",
                        AttributeType = ScalarAttributeType.S
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "MessageId",
                        AttributeType = ScalarAttributeType.S
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "DeliveryTime",
                        AttributeType = ScalarAttributeType.S
                    }
                    

                },
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = "Topic+Date",
                        KeyType = KeyType.HASH
                    },
                    new KeySchemaElement
                    {
                        AttributeName = "CreatedTime",
                        KeyType = KeyType.RANGE
                    }
                },
                GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                {
                    new GlobalSecondaryIndex
                    {
                        ProvisionedThroughput = dbConfiguration.MessageIdIndexThroughput,                        
                        IndexName = dbConfiguration.MessageIdIndexName,
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
                    },
                    new GlobalSecondaryIndex
                    {
                        ProvisionedThroughput = dbConfiguration.DeliveredIndexThroughput,
                        IndexName = dbConfiguration.DeliveredIndexName,
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement
                            {
                                AttributeName = "Topic",
                                KeyType = KeyType.HASH
                            },
                            new KeySchemaElement
                            {
                                AttributeName = "DeliveryTime",
                                KeyType = KeyType.RANGE
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
