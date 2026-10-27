#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

public class MongoDbInjectedTransactionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task When_using_the_injected_transaction_provider_should_share_the_outbox_transaction(
        bool useAsync, bool commit)
    {
        //Arrange
        var client = new MongoClient(
            Environment.GetEnvironmentVariable("BRIGHTER_MONGODB_TRANSACTION_TEST_CONNECTION_STRING")
            ?? "mongodb://localhost:27018/?replicaSet=rs0&directConnection=true");
        var databaseName = $"brighter_transaction_{Guid.NewGuid():N}";
        var database = client.GetDatabase(databaseName);
        var configuration = new MongoDbConfiguration(client, databaseName)
        {
            Outbox = new MongoDbCollectionConfiguration { Name = "outbox" }
        };
        var outbox = new MongoDbOutbox(configuration);
        var bus = new InternalBus();
        var topic = new RoutingKey("mongodb-transaction");
        var services = new ServiceCollection();
        services.AddSingleton<IAmAMongoDbConfiguration>(configuration);
        services.AddBrighter().AddProducers(options =>
        {
            options.Outbox = outbox;
            options.ConnectionProvider = typeof(MongoDbConnectionProvider);
            options.TransactionProvider = typeof(MongoDbUnitOfWork);
            options.ProducerRegistry = new InMemoryProducerRegistryFactory(bus,
                [new Publication { Topic = topic, RequestType = typeof(MongoDbTransactionCommand) }],
                InstrumentationOptions.None).Create();
        });
        using var serviceProvider = services.BuildServiceProvider();
        var processor = serviceProvider.GetRequiredService<IAmACommandProcessor>();
        var transactionProvider = serviceProvider.GetRequiredService<IAmAMongoDbTransactionProvider>();
        var command = new MongoDbTransactionCommand();

        try
        {
            await database.CreateCollectionAsync("documents");
            await database.CreateCollectionAsync("outbox");
            var documents = database.GetCollection<BsonDocument>("documents");

            //Act
            var session = Assert.IsAssignableFrom<IClientSessionHandle>(useAsync
                ? await transactionProvider.GetTransactionAsync()
                : transactionProvider.GetTransaction());
            var document = new BsonDocument("_id", command.Id.Value);
            Id messageId;
            if (useAsync)
            {
                await documents.InsertOneAsync(session, document);
                messageId = await processor.DepositPostAsync(command, transactionProvider);
            }
            else
            {
                documents.InsertOne(session, document);
                messageId = processor.DepositPost(command, transactionProvider);
            }

            //Assert
            Assert.True(transactionProvider.HasOpenTransaction);
            Assert.Equal(1L, await documents.CountDocumentsAsync(session, FilterDefinition<BsonDocument>.Empty));
            var outboxDocuments = database.GetCollection<BsonDocument>("outbox");
            Assert.Equal(1L, await outboxDocuments.CountDocumentsAsync(session, FilterDefinition<BsonDocument>.Empty));
            Assert.Equal(0L, await documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            Assert.Empty(await outbox.GetAsync());
            Assert.Empty(bus.Stream(topic));

            if (useAsync)
            {
                if (commit)
                    await transactionProvider.CommitAsync();
                else
                    await transactionProvider.RollbackAsync();
            }
            else
            {
                if (commit)
                    transactionProvider.Commit();
                else
                    transactionProvider.Rollback();
            }

            Assert.False(transactionProvider.HasOpenTransaction);
            Assert.Equal(commit ? 1L : 0L, await documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            var messages = await outbox.GetAsync();
            if (commit)
            {
                Assert.Equal(messageId, Assert.Single(messages).Id);
                if (useAsync)
                    await processor.ClearOutboxAsync([messageId]);
                else
                    processor.ClearOutbox([messageId]);
                Assert.Equal(messageId, Assert.Single(bus.Stream(topic)).Id);
            }
            else
            {
                Assert.Empty(messages);
                Assert.Empty(bus.Stream(topic));
            }
        }
        finally
        {
            transactionProvider.Close();
            await client.DropDatabaseAsync(databaseName);
        }
    }
}
