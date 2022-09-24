using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb.Extensions
{
    public class DynampDbTableQuery
    {
        public async Task<Dictionary<string, bool>> HasTables(
            IAmazonDynamoDB client,
            IEnumerable<string> tableNames,
            int pageSize = 10,
            CancellationToken ct = default(CancellationToken))
        {
            var results = tableNames.ToDictionary(tableName => tableName, tableName => false);

            string startTableName = null;
            do
            {
                var response = await client.ListTablesAsync(
                    new ListTablesRequest
                    {
                        Limit = pageSize,
                        ExclusiveStartTableName = startTableName
                    }, 
                    ct
                );
                var matches = response.TableNames.Intersect(tableNames);
                foreach (var match in matches)
                {
                    results[match] = true;
                }

                startTableName = response.LastEvaluatedTableName;
            } while (startTableName != null);

            return results;
        }
    }
}
