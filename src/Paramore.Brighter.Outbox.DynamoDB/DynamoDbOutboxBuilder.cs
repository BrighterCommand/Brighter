using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    public class DynamoDbOutboxBuilder
    {
        private readonly string _tableName = "brighter_message_store";

        public DynamoDbOutboxBuilder() { }

        public DynamoDbOutboxBuilder(string tableName)
        {
            _tableName = tableName;
        }

        public CreateTableRequest CreateOutboxTableRequest(ProvisionedThroughput tableProvisionedThroughput, ProvisionedThroughput idGlobalIndexThroughput)
        {
            if (tableProvisionedThroughput is null)
            {
                throw new ArgumentNullException(nameof(tableProvisionedThroughput));
            }

            if (idGlobalIndexThroughput is null)
            {
                idGlobalIndexThroughput = tableProvisionedThroughput;                
            }

            return new CreateTableRequest
            {
                TableName = _tableName,
                ProvisionedThroughput = tableProvisionedThroughput,
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "Topic+Date",
                        AttributeType = ScalarAttributeType.S
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "Time",
                        AttributeType = ScalarAttributeType.S
                    },
                    new AttributeDefinition
                    {
                        AttributeName = "MessageId",
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
                        AttributeName = "Time",
                        KeyType = KeyType.RANGE
                    }
                },
                GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                {
                    new GlobalSecondaryIndex
                    {
                        ProvisionedThroughput = idGlobalIndexThroughput,                        
                        IndexName = "MessageId",
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
