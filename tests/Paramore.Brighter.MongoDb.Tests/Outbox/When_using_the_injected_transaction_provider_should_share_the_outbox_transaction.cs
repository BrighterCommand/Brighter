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

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

public class MongoDbInjectedTransactionTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
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
            var session = ((IClientSessionHandle)(((IClientSessionHandle)(await Assert.That(useAsync
                ? await transactionProvider.GetTransactionAsync()
                : transactionProvider.GetTransaction()).IsAssignableTo<IClientSessionHandle>()))));
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
            await Assert.That(transactionProvider.HasOpenTransaction).IsTrue();
            await Assert.That(await documents.CountDocumentsAsync(session, FilterDefinition<BsonDocument>.Empty)).IsEqualTo(1L);
            var outboxDocuments = database.GetCollection<BsonDocument>("outbox");
            await Assert.That(await outboxDocuments.CountDocumentsAsync(session, FilterDefinition<BsonDocument>.Empty)).IsEqualTo(1L);
            await Assert.That(await documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(0L);
            await Assert.That(await outbox.GetAsync()).IsEmpty();
            await Assert.That(bus.Stream(topic)).IsEmpty();

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

            await Assert.That(transactionProvider.HasOpenTransaction).IsFalse();
            await Assert.That(await documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(commit ? 1L : 0L);
            var messages = await outbox.GetAsync();
            if (commit)
            {
                await Assert.That((await Assert.That(messages).HasSingleItem()).Id).IsEqualTo(messageId);
                if (useAsync)
                    await processor.ClearOutboxAsync([messageId]);
                else
                    processor.ClearOutbox([messageId]);
                await Assert.That((await Assert.That(bus.Stream(topic)).HasSingleItem()).Id).IsEqualTo(messageId);
            }
            else
            {
                await Assert.That(messages).IsEmpty();
                await Assert.That(bus.Stream(topic)).IsEmpty();
            }
        }
        finally
        {
            transactionProvider.Close();
            await client.DropDatabaseAsync(databaseName);
        }
    }
}
