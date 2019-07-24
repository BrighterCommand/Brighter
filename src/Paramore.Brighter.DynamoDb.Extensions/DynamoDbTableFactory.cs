#region Licence
/* The MIT License (MIT)
Copyright © 2019 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.DynamoDb.Extensions;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    /// <summary>
    /// We don't have any non-scalar types here as the .NET Low-Level API does not support creating them
    /// You can have them on a class, as the property will still be persisted to an appropriate field by the object model.
    /// Or you can create with HTTP or CLI. But the only types .NET allow you to use with the Low-Level API are B, N and S
    /// So we only look for Scalar attributes, Hash and Range Keys to create the statement.
    /// </summary>
    public class DynamoDbTableFactory
    {
        /// <summary>
        /// Builds a CreateTableRequest object from an entity type marked up with the object model; other parameters
        /// set table properties from that. Only supports string, number and byte array properties, others should
        /// be unmarked. Generally, don't create a DBProperty unless you need to use in a Filter, and rely on
        /// DynamoDb client library to figure out how to store.
        /// </summary>
        /// <param name="provisonedThroughput">What is the provisioned throughput for the table. Defaults to 10 read and write units</param>
        /// <param name="gsiProjections">How are global secondary indexes projected; defaults to all</param>
        /// <param name="billingMode">What billing mode (provisioned or request)</param>
        /// <param name="sseSpecification"></param>
        /// <param name="streamSpecification"></param>
        /// <param name="tags"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public CreateTableRequest GenerateCreateTableMapper<T>(
            DynamoDbCreateProvisionedThroughput provisonedThroughput,
            DynamoGSIProjections gsiProjections = null,
            BillingMode billingMode = null,
            SSESpecification sseSpecification = null,
            StreamSpecification streamSpecification = null,
            IEnumerable<Tag> tags = null)
        {
            var docType = typeof(T);
            string tableName = GetTableName<T>(docType);

            var createTableRequest = new CreateTableRequest(tableName, GetPrimaryKey<T>(docType).ToList());
            AddTableProvisionedThrougput<T>(provisonedThroughput, createTableRequest);
            createTableRequest.AttributeDefinitions.AddRange(GetAttributeDefinitions<T>(docType));
            createTableRequest.GlobalSecondaryIndexes.AddRange(GetGlobalSecondaryIndices<T>(docType).Select(entry => entry.Value));
            AddGSIProvisionedThroughput<T>(provisonedThroughput, createTableRequest);
            AddGSIProjections(gsiProjections, createTableRequest);
            createTableRequest.LocalSecondaryIndexes.AddRange(GetLocalSecondaryIndices<T>(docType));
            createTableRequest.BillingMode = billingMode ?? BillingMode.PROVISIONED;
            createTableRequest.SSESpecification = sseSpecification ?? new SSESpecification {Enabled = true};
            createTableRequest.StreamSpecification = streamSpecification ?? new StreamSpecification{StreamEnabled = true, StreamViewType = StreamViewType.NEW_IMAGE};
            createTableRequest.Tags = tags  != null ? tags.ToList() : new List<Tag> {new Tag{Key="outbox", Value = "brighter_outbox"}};
            return createTableRequest;
        }


        private void AddGSIProjections(DynamoGSIProjections gsiProjections, CreateTableRequest createTableRequest)
        {
            if (gsiProjections != null)
            {
                foreach (var globalSecondaryIndex in createTableRequest.GlobalSecondaryIndexes)
                {
                    if (gsiProjections.Projections.TryGetValue(globalSecondaryIndex.IndexName,
                        out Projection projection))
                    {
                        globalSecondaryIndex.Projection = projection;
                    }
                    else
                    {
                        globalSecondaryIndex.Projection = new Projection {ProjectionType = ProjectionType.ALL};
                    }
                }  
            }
        }

        private void AddGSIProvisionedThroughput<T>(DynamoDbCreateProvisionedThroughput provisonedThroughput,
            CreateTableRequest createTableRequest)
        {
            if (provisonedThroughput != null)
            {
                foreach (var globalSecondaryIndex in createTableRequest.GlobalSecondaryIndexes)
                {
                    if (provisonedThroughput.GSIThroughputs.TryGetValue(globalSecondaryIndex.IndexName,
                        out ProvisionedThroughput gsiProvisonedThroughput))
                    {
                        globalSecondaryIndex.ProvisionedThroughput = gsiProvisonedThroughput;
                    }
                    else
                    {
                        globalSecondaryIndex.ProvisionedThroughput = new ProvisionedThroughput{ReadCapacityUnits = 10, WriteCapacityUnits = 10};
                    }
                }
            }
        }

        private void AddTableProvisionedThrougput<T>(DynamoDbCreateProvisionedThroughput provisonedThroughput,
            CreateTableRequest createTableRequest)
        {
            if (provisonedThroughput != null)
            {
                createTableRequest.ProvisionedThroughput = provisonedThroughput.Table;
            }
        }

        private List<AttributeDefinition> GetAttributeDefinitions<T>(Type docType)
        {
            //attributes
            var fields = from prop in docType.GetProperties()
                from attribute in prop.GetCustomAttributesData()
                where attribute.AttributeType == typeof(DynamoDBPropertyAttribute)
                select new {prop, attribute};

            var attributeDefinitions = new List<AttributeDefinition>();
            foreach (var item in fields)
            {
                string attributeName = item.attribute.ConstructorArguments.Count == 0
                    ? item.prop.Name
                    : (string) item.attribute.ConstructorArguments.FirstOrDefault().Value;

                attributeDefinitions.Add(new AttributeDefinition(attributeName, GetDynamoDbType(item.prop.PropertyType)));
            }

            return attributeDefinitions;
        }

        private Dictionary<string, GlobalSecondaryIndex> GetGlobalSecondaryIndices<T>(Type docType)
        {
            //global secondary indexes
            var gsiMap = new Dictionary<string, GlobalSecondaryIndex>();

            var gsiHashKeyResults = from prop in docType.GetProperties()
                from attribute in prop.GetCustomAttributesData()
                where attribute.AttributeType == typeof(DynamoDBGlobalSecondaryIndexHashKeyAttribute)
                select new {prop, attribute};

            foreach (var gsiHashKeyResult in gsiHashKeyResults)
            {
                var gsi = new GlobalSecondaryIndex();
                gsi.IndexName = gsiHashKeyResult.attribute.ConstructorArguments.Count == 0
                    ? gsiHashKeyResult.prop.Name
                    : (string) gsiHashKeyResult.attribute.ConstructorArguments.FirstOrDefault().Value;

                var gsiHashKey = new KeySchemaElement(gsiHashKeyResult.prop.Name, KeyType.HASH);
                gsi.KeySchema.Add(gsiHashKey);

                gsiMap.Add(gsi.IndexName, gsi);
            }

            var gsiRangeKeyResults = from prop in docType.GetProperties()
                from attribute in prop.GetCustomAttributesData()
                where attribute.AttributeType == typeof(DynamoDBGlobalSecondaryIndexRangeKeyAttribute)
                select new {prop, attribute};

            foreach (var gsiRangeKeyResult in gsiRangeKeyResults)
            {
                var indexName = gsiRangeKeyResult.attribute.ConstructorArguments.Count == 0
                    ? gsiRangeKeyResult.prop.Name
                    : (string) gsiRangeKeyResult.attribute.ConstructorArguments.FirstOrDefault().Value;

                if (!gsiMap.TryGetValue(indexName, out GlobalSecondaryIndex entry))
                    throw new InvalidOperationException(
                        $"The global secondary index {gsiRangeKeyResult.prop.Name} lacks a hash key");

                var gsiRangeKey = new KeySchemaElement(gsiRangeKeyResult.prop.Name, KeyType.RANGE);
                entry.KeySchema.Add(gsiRangeKey);
            }

            return gsiMap;
        }

        private static List<LocalSecondaryIndex> GetLocalSecondaryIndices<T>(Type docType)
        {
            //local secondary indexes
            var lsiList = new List<LocalSecondaryIndex>();

            var lsiRangeKeyResults = from prop in docType.GetProperties()
                from attribute in prop.GetCustomAttributesData()
                where attribute.AttributeType == typeof(DynamoDBLocalSecondaryIndexRangeKeyAttribute)
                select new {prop, attribute};

            foreach (var lsiRangeKeyResult in lsiRangeKeyResults)
            {
                var indexName = lsiRangeKeyResult.attribute.ConstructorArguments.Count == 0
                    ? lsiRangeKeyResult.prop.Name
                    : (string) lsiRangeKeyResult.attribute.ConstructorArguments.FirstOrDefault().Value;

                var lsi = new LocalSecondaryIndex();
                lsi.IndexName = indexName;
                lsi.KeySchema.Add(new KeySchemaElement(lsiRangeKeyResult.prop.Name, KeyType.RANGE));
                lsiList.Add(lsi);
            }

            return lsiList;
        }
        
        private IEnumerable<KeySchemaElement> GetPrimaryKey<T>(Type docType)
        {
            //hash key
            var hashKey = (from prop in docType.GetProperties()
                from attribute in prop.GetCustomAttributesData()
                where attribute.AttributeType == typeof(DynamoDBHashKeyAttribute)
                select new KeySchemaElement(prop.Name, KeyType.HASH)).FirstOrDefault();

            if (hashKey == null)
            {
                throw new InvalidOperationException("The type myst have a primary key mapped with DynamoDBHashKey");
            }

            var rangeKey = from prop in docType.GetProperties()
                from attribute in prop.GetCustomAttributesData()
                where attribute.AttributeType == typeof(DynamoDBRangeKeyAttribute)
                select new KeySchemaElement(prop.Name, KeyType.RANGE);

            var index = new List<KeySchemaElement> {hashKey}.Concat(rangeKey);
            return index;
        }

        private string GetTableName<T>(Type docType)
        {
            var tableAttribute = docType.GetCustomAttributesData()
                .FirstOrDefault(attr => attr.AttributeType == typeof(DynamoDBTableAttribute));
            if (tableAttribute == null)
                throw new InvalidOperationException("Types to be mapped must have the DynamoDbTableAttribute");

            string tableName = tableAttribute.ConstructorArguments.Count == 0
                ? docType.Name
                : (string) tableAttribute.ConstructorArguments.FirstOrDefault().Value;
            return tableName;
        }

        // We treat all primitive types as a number
        // Then we test for a string, and treat that explicitly as a string
        // If not we look for a byte array and treat it as binary
        // Everything else is unsupported in .NET
        private ScalarAttributeType GetDynamoDbType(Type propertyType)
        {
            if (propertyType.IsPrimitive)
            {
                return ScalarAttributeType.N;
            }

            if (propertyType == typeof(string))
            {
                return ScalarAttributeType.S;
            }

            if (propertyType == typeof(byte[]))
            {
                return ScalarAttributeType.B;
            }

            throw new NotSupportedException($"We can't convert {propertyType.Name} to a DynamoDb type. Avoid marking as an attribute and see if the lib can figure it out");
        }

   }
}
