using System.Collections.Generic;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.MessageStore.DynamoDB
{
    public class DynamoDbMessageStoreBuilder
    {
        private readonly string _tableName = "brighter_message_store";

        public DynamoDbMessageStoreBuilder() { }

        public DynamoDbMessageStoreBuilder(string tableName)
        {
            _tableName = tableName;
        }

        public CreateTableRequest CreateMessageStoreTableRequest(int readCapacityUnits = 2, int writeCapacityUnits = 1)
        {
            return new CreateTableRequest
            {
                TableName = _tableName,
                ProvisionedThroughput = new ProvisionedThroughput
                {
                    ReadCapacityUnits = readCapacityUnits,
                    WriteCapacityUnits = writeCapacityUnits
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition
                    {
                        AttributeName = "Id",
                        AttributeType = ScalarAttributeType.S
                    }
                },
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement
                    {
                        AttributeName = "Id",
                        KeyType = KeyType.HASH
                    }
                }
            };
        }
    }
}
