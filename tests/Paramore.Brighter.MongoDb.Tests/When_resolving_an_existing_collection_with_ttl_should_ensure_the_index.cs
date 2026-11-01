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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Paramore.Brighter.Inbox.MongoDb;
using Paramore.Brighter.Locking.MongoDb;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests;

public class MongoDbCollectionTtlIndexTests : IDisposable
{
    private const string COLLECTION_NAME = "messages";
    private const string INDEX_NAME = "brighter_ttl_messages";
    private readonly string _databaseName = $"brighter_ttl_{Guid.NewGuid():N}";
    private readonly IMongoDatabase _database;

    public MongoDbCollectionTtlIndexTests()
    {
        _database = Const.Client.GetDatabase(_databaseName);
    }

    public static IEnumerable<object[]> Providers()
    {
        foreach (var provider in new[] { "OutboxSync", "OutboxAsync", "InboxSync", "InboxAsync", "Locking" })
            yield return new object[] { provider };
    }

    public static IEnumerable<object[]> ResolutionModes()
    {
        foreach (var provider in Providers())
        foreach (var mode in new[]
        {
            OnResolvingACollection.Assume,
            OnResolvingACollection.Validate,
            OnResolvingACollection.CreateIfNotExists
        })
            yield return new object[] { provider[0], mode };
    }

    public static IEnumerable<object[]> CreationModes()
        => ResolutionModes().Where(row => (OnResolvingACollection)row[1] != OnResolvingACollection.Validate);

    [Theory]
    [MemberData(nameof(ResolutionModes))]
    public async Task When_resolving_an_existing_collection_with_ttl_should_ensure_the_index(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        await _database.CreateCollectionAsync(COLLECTION_NAME);
        var configuration = CreateConfiguration(mode, TimeSpan.FromHours(1));
        var write = CreateWriter(provider, configuration);

        //Act
        await write();
        await write();
        await CreateWriter(provider, configuration)();

        //Assert
        await AssertTtlIndexAsync();
        Assert.Equal(3L, await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [MemberData(nameof(CreationModes))]
    public async Task When_resolving_a_missing_collection_with_ttl_should_ensure_the_index(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        var configuration = CreateConfiguration(mode, TimeSpan.FromHours(1));
        var write = CreateWriter(provider, configuration);

        //Act
        await write();
        await CreateWriter(provider, configuration)();

        //Assert
        await AssertTtlIndexAsync();
        Assert.Equal(2L, await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [MemberData(nameof(ResolutionModes))]
    public async Task When_ttl_is_not_configured_should_leave_the_collection_without_a_ttl_index(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        await _database.CreateCollectionAsync(COLLECTION_NAME);
        var write = CreateWriter(provider, CreateConfiguration(mode, null));

        //Act
        await write();

        //Assert
        using var cursor = await Documents.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        Assert.DoesNotContain(indexes, index => index.Contains("expireAfterSeconds"));
        Assert.Equal(1L, await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public async Task When_validating_a_missing_collection_with_ttl_should_fail_without_creating_it(string provider)
    {
        //Arrange
        var write = CreateWriter(provider,
            CreateConfiguration(OnResolvingACollection.Validate, TimeSpan.FromHours(1)));

        //Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(write);
        using var cursor = await _database.ListCollectionNamesAsync();
        Assert.Empty(await cursor.ToListAsync());
    }

    [Theory]
    [MemberData(nameof(ResolutionModes))]
    public async Task When_ttl_index_creation_fails_should_retry_before_allowing_writes(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        await _database.CreateCollectionAsync(COLLECTION_NAME);
        await Documents.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("TimeStamp"),
            new CreateIndexOptions { Name = INDEX_NAME, ExpireAfter = TimeSpan.FromHours(2) }));
        var write = CreateWriter(provider, CreateConfiguration(mode, TimeSpan.FromHours(1)));

        //Act + Assert
        await Assert.ThrowsAsync<MongoCommandException>(write);
        await Assert.ThrowsAsync<MongoCommandException>(write);
        Assert.Equal(0L, await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
        using (var cursor = await Documents.Indexes.ListAsync())
        {
            var index = Assert.Single(await cursor.ToListAsync(), value => value["name"] == INDEX_NAME);
            Assert.Equal(7200L, index["expireAfterSeconds"].ToInt64());
        }

        await Documents.Indexes.DropOneAsync(INDEX_NAME);
        await write();
        await AssertTtlIndexAsync();
        Assert.Equal(1L, await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [MemberData(nameof(ResolutionModes))]
    public async Task When_callers_resolve_the_same_collection_concurrently_should_all_succeed_with_one_ttl_index(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        const int CALLERS = 8;
        var createCollection = mode == OnResolvingACollection.CreateIfNotExists;
        if (!createCollection)
            await _database.CreateCollectionAsync(COLLECTION_NAME);

        using var barrier = new Barrier(CALLERS);
        var arrivals = 0;
        var timeouts = 0;
        var settings = MongoClientSettings.FromConnectionString(Const.ConnectionString);
        settings.ClusterConfigurator = builder =>
        {
            if (createCollection)
            {
                builder.Subscribe<CommandSucceededEvent>(command =>
                {
                    if (command.CommandName != "listCollections" ||
                        command.Reply["cursor"]["firstBatch"].AsBsonArray.Count != 0)
                        return;

                    Interlocked.Increment(ref arrivals);
                    if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                        Interlocked.Increment(ref timeouts);
                });
            }
            else
            {
                builder.Subscribe<CommandStartedEvent>(command =>
                {
                    if (command.CommandName != "createIndexes")
                        return;

                    Interlocked.Increment(ref arrivals);
                    if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                        Interlocked.Increment(ref timeouts);
                });
            }
        };
        using var client = new MongoClient(settings);
        var configuration = CreateConfiguration(mode, TimeSpan.FromHours(1));
        configuration.Client = client;
        var writers = Enumerable.Range(0, CALLERS).Select(_ => CreateWriter(provider, configuration)).ToArray();

        //Act
        var exception = await Record.ExceptionAsync(() => Task.WhenAll(writers.Select(write =>
            Task.Factory.StartNew(write, CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap())));

        //Assert
        Assert.Null(exception);
        await AssertTtlIndexAsync();
        Assert.Equal(CALLERS, arrivals);
        Assert.Equal(0, timeouts);
        Assert.Equal((long)CALLERS, await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }

    [Theory]
    [MemberData(nameof(ResolutionModes))]
    public async Task When_index_permission_is_missing_should_fail_before_writing_and_retry_after_permission_is_granted(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        const string ROLE = "document_writer";
        const string USER = "ttl_writer";
        const string PASSWORD = "local-test-password";
        await _database.CreateCollectionAsync(COLLECTION_NAME);
        await _database.RunCommandAsync<BsonDocument>(new BsonDocument
        {
            { "createRole", ROLE },
            { "privileges", new BsonArray
                {
                    new BsonDocument
                    {
                        { "resource", new BsonDocument { { "db", _databaseName }, { "collection", COLLECTION_NAME } } },
                        { "actions", new BsonArray { "insert", "find" } }
                    },
                    new BsonDocument
                    {
                        { "resource", new BsonDocument { { "db", _databaseName }, { "collection", "" } } },
                        { "actions", new BsonArray { "listCollections" } }
                    }
                }
            },
            { "roles", new BsonArray() }
        });
        try
        {
            await _database.RunCommandAsync<BsonDocument>(new BsonDocument
            {
                { "createUser", USER }, { "pwd", PASSWORD }, { "roles", new BsonArray { ROLE } }
            });
            try
            {
                var settings = MongoClientSettings.FromConnectionString(Const.ConnectionString);
                settings.Credential = MongoCredential.CreateCredential(_databaseName, USER, PASSWORD);
                using var client = new MongoClient(settings);
                var configuration = CreateConfiguration(mode, TimeSpan.FromHours(1));
                configuration.Client = client;
                var write = CreateWriter(provider, configuration);
                await client.GetDatabase(_databaseName).GetCollection<BsonDocument>(COLLECTION_NAME)
                    .InsertOneAsync(new BsonDocument("_id", "permission-probe"));
                await Documents.DeleteOneAsync(new BsonDocument("_id", "permission-probe"));

                //Act + Assert
                var firstFailure = await Assert.ThrowsAsync<MongoCommandException>(write);
                var secondFailure = await Assert.ThrowsAsync<MongoCommandException>(write);
                Assert.Equal(13, firstFailure.Code);
                Assert.Equal(13, secondFailure.Code);
                Assert.Equal(0L, await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
                using (var cursor = await Documents.Indexes.ListAsync())
                    Assert.DoesNotContain(await cursor.ToListAsync(), index => index.Contains("expireAfterSeconds"));

                await _database.RunCommandAsync<BsonDocument>(new BsonDocument
                {
                    { "grantPrivilegesToRole", ROLE },
                    { "privileges", new BsonArray
                        {
                            new BsonDocument
                            {
                                { "resource", new BsonDocument { { "db", _databaseName }, { "collection", COLLECTION_NAME } } },
                                { "actions", new BsonArray { "createIndex" } }
                            }
                        }
                    }
                });
                await write();
                await AssertTtlIndexAsync();
                Assert.Equal(1L, await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
            }
            finally
            {
                await _database.RunCommandAsync<BsonDocument>(new BsonDocument("dropUser", USER));
            }
        }
        finally
        {
            await _database.RunCommandAsync<BsonDocument>(new BsonDocument("dropRole", ROLE));
        }
    }

    private IMongoCollection<BsonDocument> Documents => _database.GetCollection<BsonDocument>(COLLECTION_NAME);

    private MongoDbConfiguration CreateConfiguration(OnResolvingACollection mode, TimeSpan? ttl)
    {
        var collection = new MongoDbCollectionConfiguration
        {
            Name = COLLECTION_NAME,
            MakeCollection = mode,
            TimeToLive = ttl
        };
        return new MongoDbConfiguration(Const.Client, _databaseName)
        {
            Outbox = collection,
            Inbox = collection,
            Locking = collection
        };
    }

    private static Func<Task> CreateWriter(string provider, MongoDbConfiguration configuration)
    {
        switch (provider)
        {
            case "OutboxSync":
                var outbox = new MongoDbOutbox(configuration);
                return () =>
                {
                    outbox.Add(CreateMessage(), null);
                    return Task.CompletedTask;
                };
            case "OutboxAsync":
                var asyncOutbox = new MongoDbOutbox(configuration);
                return () => asyncOutbox.AddAsync(CreateMessage(), null);
            case "InboxSync":
                var inbox = new MongoDbInbox(configuration);
                return () =>
                {
                    inbox.Add(new MyCommand(), "ttl-context", null);
                    return Task.CompletedTask;
                };
            case "InboxAsync":
                var asyncInbox = new MongoDbInbox(configuration);
                return () => asyncInbox.AddAsync(new MyCommand(), "ttl-context", null);
            case "Locking":
                var locking = new MongoDbLockingProvider(configuration);
                return async () => Assert.NotNull(
                    await locking.ObtainLockAsync(Id.Random().Value, CancellationToken.None));
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }
    }

    private static Message CreateMessage()
        => new(new MessageHeader(Id.Random(), new RoutingKey("ttl-topic"), MessageType.MT_EVENT),
            new MessageBody("payload"));

    private async Task AssertTtlIndexAsync()
    {
        using var cursor = await Documents.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        var ttlIndex = Assert.Single(indexes, index => index.Contains("expireAfterSeconds"));
        Assert.Equal(INDEX_NAME, ttlIndex["name"].AsString);
        Assert.Equal(3600L, ttlIndex["expireAfterSeconds"].ToInt64());
    }

    public void Dispose() => Const.Client.DropDatabase(_databaseName);
}
