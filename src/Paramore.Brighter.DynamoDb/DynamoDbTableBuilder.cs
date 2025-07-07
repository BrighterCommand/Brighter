using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb
{
    public class DynamoDbTableBuilder
    {
        private readonly IAmazonDynamoDB _client;

        public DynamoDbTableBuilder(IAmazonDynamoDB client)
        {
            _client = client;
        }
        
        /// <summary>
        /// Build a table from a create table request
        /// We filter out any attributes that are not B, S, or N because DynamoDB does not currently support
        /// creation of these types via the API. As these are not used for hash or range keys they are not
        /// required to store items in DynamoDB
        /// </summary>
        /// <param name="createTableRequest">The request to build tables from</param>
        /// <param name="ct"></param>
        /// <returns>The response to table creation</returns>
        public async Task<CreateTableResponse> Build(CreateTableRequest createTableRequest, CancellationToken ct = default)
        {
            var modifiedTableRequest = RemoveNonSchemaAttributes(createTableRequest);
            return await _client.CreateTableAsync(modifiedTableRequest, ct);
        }

        /// <summary>
        /// Delete the specified tables. Note that tables will be in TableStatus.Deleting until gone
        /// </summary>
        /// <param name="tableNames">The list of tables to delete</param>
        /// <param name="ct">A cancellation token</param>
        /// <returns>The response to table deletion</returns>
        public async Task<DeleteTableResponse[]> Delete(IEnumerable<string> tableNames, CancellationToken ct = default)
        {
            var allDeletes = tableNames.Select(tn => _client.DeleteTableAsync(tn, ct)).ToList();
            return await Task.WhenAll(allDeletes);
        }
        
        //EnsureTablesGone. Deleting until cannot be found
        public async Task EnsureTablesDeleted(IEnumerable<string> tableNames, CancellationToken ct = default)
        {
            Dictionary<string, bool> tableResults = null;
            do
            {
                var tableQuery = new DynamoDbTableQuery();
                tableResults = await tableQuery.HasTables(_client, tableNames, ct: ct);
            } while (tableResults.Any(tr => tr.Value));
        }

        /// <summary>
        /// Ensures, due to the asynchronous nature of DDL operations that a table is ready i.e. in desired state
        /// </summary>
        /// <param name="tableNames">The tables to check</param>
        /// <param name="targetStatus">The status that defines ready</param>
        /// <param name="ct">A cancellation token</param>
        /// <returns></returns>
        public async Task EnsureTablesReady(IEnumerable<string> tableNames, TableStatus targetStatus, CancellationToken ct = default)
        {
            // Let us wait until all tables are created. Call DescribeTable.
            var tableStates = (from tableName in tableNames
                select new DynamoDbTableStatus{TableName = tableName, IsReady = false}).ToList();
            
            do
            {
                await Task.Delay(5000, ct);
                try
                {
                    var allQueries = tableStates.Where(ts => ts.IsReady == false).Select(ts => _client.DescribeTableAsync(ts.TableName, ct));
                    var allResults = await Task.WhenAll(allQueries);

                    foreach (var result in allResults)
                    {
                        if (result.Table.TableStatus == targetStatus)
                        {
                            var tableStatus = tableStates.First(ts => ts.TableName == result.Table.TableName);
                            tableStatus.IsReady = true;
                        }
                    }

                }
                catch (ResourceNotFoundException)
                {
                    // DescribeTable is eventually consistent. So you might
                    // get resource not found. So we handle the potential exception.
                }
            } while (tableStates.Any(ts => !ts.IsReady));
        }
        
        public async Task<(bool exist, IEnumerable<string> missing)> HasTables(IEnumerable<string> tableNames, CancellationToken ct = default)
        {
            
            var tableCheck = tableNames.ToDictionary(tableName => tableName, tableName => false);
            
            string lastEvaluatedTableName = null;
            do
            {
                var tablesResponse = await _client.ListTablesAsync(ct);

                foreach (var tableName in tablesResponse.TableNames)
                {
                    if (tableCheck.ContainsKey(tableName))
                        tableCheck[tableName] = true;
                }
                
                lastEvaluatedTableName = tablesResponse.LastEvaluatedTableName;
            } while (lastEvaluatedTableName != null);

            return tableCheck.Any(kv => kv.Value) ? 
                (true, tableCheck.Where(tbl => tbl.Value).Select(tbl => tbl.Key)) : 
                (false, []);

        }
        
        public CreateTableRequest RemoveNonSchemaAttributes(CreateTableRequest tableRequest)
        {
            var keyMatchedAttributes = new List<AttributeDefinition>();
            
            //get the unfiltered markup
            var existingAttributes = tableRequest.AttributeDefinitions;

            foreach (var attribute in existingAttributes)
            {
                var added = AddKeyUsedFields(tableRequest, attribute, keyMatchedAttributes);

                if (!added)
                {
                     added = AddGlobalSecondaryIndexUsedFields(tableRequest, attribute, keyMatchedAttributes);
                }

                if (!added)
                {
                    AddLocalSecondaryIndexUsedFields(tableRequest, attribute, keyMatchedAttributes);
                }
            }

            //swap for the filtered list
            tableRequest.AttributeDefinitions = keyMatchedAttributes;
            
            return tableRequest;
        }

        private static bool AddLocalSecondaryIndexUsedFields(CreateTableRequest tableRequest, AttributeDefinition attribute,
            List<AttributeDefinition> keyMatchedAttributes)
        {
            foreach (var index in tableRequest.LocalSecondaryIndexes)
            {
                foreach (var keySchemaElement in index.KeySchema)
                {
                    if (keySchemaElement.AttributeName == attribute.AttributeName)
                    {
                        keyMatchedAttributes.Add(attribute);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool AddGlobalSecondaryIndexUsedFields(CreateTableRequest tableRequest, AttributeDefinition attribute,
            List<AttributeDefinition> keyMatchedAttributes)
        {
            foreach (var index in tableRequest.GlobalSecondaryIndexes)
            {
                foreach (var keySchemaElement in index.KeySchema)
                {
                    if (keySchemaElement.AttributeName == attribute.AttributeName)
                    {
                        keyMatchedAttributes.Add(attribute);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool AddKeyUsedFields(CreateTableRequest tableRequest, AttributeDefinition attribute,
            List<AttributeDefinition> keyMatchedAttributes)
        {
            foreach (var keySchemaElement in tableRequest.KeySchema)
            {
                if (keySchemaElement.AttributeName == attribute.AttributeName)
                {
                    keyMatchedAttributes.Add(attribute);
                    return true;
                }
            }

            return false;
        }

        private sealed class DynamoDbTableStatus
        { 
            public string TableName { get; init; }
            public bool IsReady { get; set; }
        }
   }
}
