#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter.CommandStore.DynamoDB;
using Paramore.Brighter.MessageStore.DynamoDB;

namespace Paramore.Brighter.Tests
{
    public class DynamoDbTestHelper
    {
        public DynamoDBContext DynamoDbContext { get; }
        public DynamoDbCommandStoreConfiguration DynamoDbCommandStoreTestConfiguration { get; }
        public DynamoDbMessageStoreConfiguration DynamoDbMessageStoreTestConfiguration { get; }
        
        public AmazonDynamoDBClient Client { get; }

        public DynamoDbTestHelper()
        {
            var clientConfig = new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost:8000"
            };

            Client = new AmazonDynamoDBClient(clientConfig);
            var tableName = $"test_{Guid.NewGuid()}";            

            DynamoDbContext = new DynamoDBContext(Client);
            DynamoDbCommandStoreTestConfiguration = new DynamoDbCommandStoreConfiguration($"command_{tableName}", true, "CommandId", "ContextKey");
            DynamoDbMessageStoreTestConfiguration = new DynamoDbMessageStoreConfiguration($"message_{tableName}", true, "MessageId");
        }

        public void CreateMessageStoreTable(CreateTableRequest request)
        {           
            Client.CreateTableAsync(request).GetAwaiter().GetResult();

            WaitUntilTableReady(DynamoDbMessageStoreTestConfiguration.TableName);
        }

        public void CreateCommandStoreTable(CreateTableRequest request)
        {
            Client.CreateTableAsync(request).GetAwaiter().GetResult();

            WaitUntilTableReady(DynamoDbCommandStoreTestConfiguration.TableName);
        }

        public async Task<IEnumerable<DynamoDbMessage>> Scan()
        {
            return await DynamoDbContext.ScanAsync<DynamoDbMessage>(new List<ScanCondition>(), new DynamoDBOperationConfig {OverrideTableName = DynamoDbMessageStoreTestConfiguration.TableName})
                                        .GetNextSetAsync()
                                        .ConfigureAwait(false);
        }

        private void WaitUntilTableReady(string tableName)
        {
            string status = null;
            do
            {
                System.Threading.Thread.Sleep(500);
                try
                {
                    var res = Client.DescribeTableAsync(new DescribeTableRequest
                    {
                        TableName = tableName
                    }).GetAwaiter().GetResult();
                    
                    status = res.Table.TableStatus;
                }
                catch (ResourceNotFoundException) { }
            } while (status != "ACTIVE");
        }

        public void CleanUpCommandDb()
        {
            Client.DeleteTableAsync(DynamoDbCommandStoreTestConfiguration.TableName).GetAwaiter().GetResult();
        }

        public void CleanUpMessageDb()
        {
            Client.DeleteTableAsync(DynamoDbMessageStoreTestConfiguration.TableName).Wait();
        }
    }     
}
