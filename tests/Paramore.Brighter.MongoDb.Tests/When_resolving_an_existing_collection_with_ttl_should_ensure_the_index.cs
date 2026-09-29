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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Paramore.Brighter.Inbox.MongoDb;
using Paramore.Brighter.Locking.MongoDb;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MongoDb;

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

    [Test]
    [MethodDataSource(nameof(ResolutionModes))]
    public async Task When_writing_a_document_with_ttl_should_index_a_bson_date(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        await _database.CreateCollectionAsync(COLLECTION_NAME);
        var configuration = CreateConfiguration(mode, TimeSpan.FromHours(1));
        var write = CreateWriter(provider, configuration);
        var expectedTimestamp = new DateTimeOffset(2100, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5)).AddTicks(1234);
        configuration.TimeProvider = new FakeTimeProvider(expectedTimestamp);

        //Act
        await write();

        //Assert
        using var cursor = await Documents.Indexes.ListAsync();
        var ttlIndex = await Assert.That(System.Linq.Enumerable.Where(await cursor.ToListAsync(), index => index.Contains("expireAfterSeconds"))).HasSingleItem();
        var indexedField = (await Assert.That(ttlIndex["key"].AsBsonDocument.Elements).HasSingleItem()).Name;
        BsonValue indexedValue = await Documents.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        foreach (var field in indexedField.Split('.'))
        {
            await Assert.That(indexedValue).IsTypeOf<BsonDocument>();
        }

        var timestamp = await Assert.That(indexedValue).IsTypeOf<BsonDateTime>();
        await Assert.That(timestamp).IsEqualTo(new BsonDateTime(expectedTimestamp.UtcDateTime));
        await Assert.That(ttlIndex["expireAfterSeconds"].ToInt64()).IsEqualTo(3600L);
    }

    [Test]
    [MethodDataSource(nameof(ResolutionModes))]
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
        await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(3L);
    }

    [Test]
    [MethodDataSource(nameof(CreationModes))]
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
        await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(2L);
    }

    [Test]
    [MethodDataSource(nameof(ResolutionModes))]
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
        await Assert.That(System.Linq.Enumerable.Any(indexes, index => index.Contains("expireAfterSeconds"))).IsFalse();
        await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(1L);
    }

    [Test]
    [MethodDataSource(nameof(Providers))]
    public async Task When_validating_a_missing_collection_with_ttl_should_fail_without_creating_it(string provider)
    {
        //Arrange
        var write = CreateWriter(provider,
            CreateConfiguration(OnResolvingACollection.Validate, TimeSpan.FromHours(1)));

        //Act + Assert
        await Assert.That(write).ThrowsExactly<InvalidOperationException>();
        using var cursor = await _database.ListCollectionNamesAsync();
        await Assert.That(await cursor.ToListAsync()).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(ResolutionModes))]
    public async Task When_ttl_index_creation_fails_should_retry_before_allowing_writes(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        await _database.CreateCollectionAsync(COLLECTION_NAME);
        await Documents.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("TimeStamp.DateTime"),
            new CreateIndexOptions { Name = INDEX_NAME, ExpireAfter = TimeSpan.FromHours(2) }));
        var write = CreateWriter(provider, CreateConfiguration(mode, TimeSpan.FromHours(1)));

        //Act + Assert
        await Assert.That(write).ThrowsExactly<MongoCommandException>();
        await Assert.That(write).ThrowsExactly<MongoCommandException>();
        await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(0L);
        using (var cursor = await Documents.Indexes.ListAsync())
        {
            var index = await Assert.That(System.Linq.Enumerable.Where(await cursor.ToListAsync(), value => value["name"] == INDEX_NAME)).HasSingleItem();
            await Assert.That(index["expireAfterSeconds"].ToInt64()).IsEqualTo(7200L);
        }

        await Documents.Indexes.DropOneAsync(INDEX_NAME);
        await write();
        await AssertTtlIndexAsync();
        await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(1L);
    }

    [Test]
    [MethodDataSource(nameof(ResolutionModes))]
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
        var exception = await TestExceptionRecorder.CaptureAsync(() => Task.WhenAll(writers.Select(write =>
            Task.Factory.StartNew(write, CancellationToken.None, TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap())));

        //Assert
        await Assert.That(exception).IsNull();
        await AssertTtlIndexAsync();
        await Assert.That(arrivals).IsEqualTo(CALLERS);
        await Assert.That(timeouts).IsEqualTo(0);
        await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo((long)CALLERS);
    }

    [Test]
    [MethodDataSource(nameof(ResolutionModes))]
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
                var firstFailure = await Assert.That(write).ThrowsExactly<MongoCommandException>();
                var secondFailure = await Assert.That(write).ThrowsExactly<MongoCommandException>();
                await Assert.That(firstFailure.Code).IsEqualTo(13);
                await Assert.That(secondFailure.Code).IsEqualTo(13);
                await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(0L);
                using (var cursor = await Documents.Indexes.ListAsync())
                    await Assert.That(System.Linq.Enumerable.Any(await cursor.ToListAsync(), index => index.Contains("expireAfterSeconds"))).IsFalse();

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
                await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(1L);
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

    [Test]
    [Arguments(OnResolvingACollection.Assume)]
    [Arguments(OnResolvingACollection.Validate)]
    [Arguments(OnResolvingACollection.CreateIfNotExists)]
    public async Task When_a_timestamp_is_stored_as_a_bson_date_should_index_the_mapped_field(
        OnResolvingACollection mode)
    {
        //Arrange
        await _database.CreateCollectionAsync(COLLECTION_NAME);
        var store = new MongoDbDateTtlStore(CreateConfiguration(mode, TimeSpan.FromHours(1)));
        var timestamp = new DateTimeOffset(2100, 1, 2, 3, 4, 5, TimeSpan.FromHours(5.5));

        //Act
        store.Add(new MongoDbDateTtlDocument { TimeStamp = timestamp });

        //Assert
        await AssertTtlIndexAsync();
        using var cursor = await Documents.Indexes.ListAsync();
        var ttlIndex = await Assert.That(System.Linq.Enumerable.Where(await cursor.ToListAsync(), index => index.Contains("expireAfterSeconds"))).HasSingleItem();
        await Assert.That(ttlIndex["key"]).IsEqualTo(new BsonDocument("created_at", 1));
        var document = await Documents.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        await Assert.That(document["created_at"]).IsEqualTo(new BsonDateTime(timestamp.UtcDateTime));
    }

    [Test]
    [MethodDataSource(nameof(ResolutionModes))]
    public async Task When_the_legacy_ttl_index_is_recreated_should_resume_writes_without_changing_existing_documents(
        string provider, OnResolvingACollection mode)
    {
        //Arrange
        await _database.CreateCollectionAsync(COLLECTION_NAME);
        var configuration = CreateConfiguration(mode, null);
        await CreateWriter(provider, configuration)();
        var original = await Documents.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync();
        await Documents.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("TimeStamp"),
            new CreateIndexOptions { Name = INDEX_NAME, ExpireAfter = TimeSpan.FromHours(1) }));
        configuration.Outbox!.TimeToLive = TimeSpan.FromHours(1);
        var write = CreateWriter(provider, configuration);

        //Act + Assert
        await Assert.That(write).ThrowsExactly<MongoCommandException>();
        await Assert.That(write).ThrowsExactly<MongoCommandException>();
        await Assert.That(await Documents.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync()).IsEqualTo(original);
        using (var cursor = await Documents.Indexes.ListAsync())
        {
            var index = await Assert.That(System.Linq.Enumerable.Where(await cursor.ToListAsync(), item => item["name"] == INDEX_NAME)).HasSingleItem();
            await Assert.That(index["key"]).IsEqualTo(new BsonDocument("TimeStamp", 1));
            await Assert.That(index["expireAfterSeconds"].ToInt64()).IsEqualTo(3600L);
        }

        await Documents.Indexes.DropOneAsync(INDEX_NAME);
        await write();
        await AssertTtlIndexAsync();
        await Assert.That(await Documents.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty)).IsEqualTo(2L);
        await Assert.That(await Documents.Find(new BsonDocument("_id", original["_id"])).SingleAsync()).IsEqualTo(original);
    }

    [Test]
    public async Task When_the_ttl_monitor_runs_should_remove_expired_documents_and_allow_lock_reacquisition()
    {
        //Arrange
        var configurations = new List<(string Provider, MongoDbConfiguration Configuration, BsonValue ExpiredId, BsonValue RetainedId)>();
        var now = DateTimeOffset.UtcNow;
        foreach (var provider in Providers().Select(row => (string)row[0]))
        {
            var configuration = CreateConfiguration(OnResolvingACollection.CreateIfNotExists, null);
            configuration.Outbox!.Name = provider;
            var clock = new FakeTimeProvider(now - TimeSpan.FromHours(2));
            configuration.TimeProvider = clock;
            var write = CreateWriter(provider, configuration);
            var documents = _database.GetCollection<BsonDocument>(provider);
            await write();
            var expiredId = (await documents.Find(FilterDefinition<BsonDocument>.Empty).SingleAsync())["_id"];
            clock.Advance(TimeSpan.FromHours(2));
            await write();
            var retainedId = (await documents.Find(new BsonDocument("_id", new BsonDocument("$ne", expiredId))).SingleAsync())["_id"];
            configurations.Add((provider, configuration, expiredId, retainedId));
        }

        //Act
        foreach (var entry in configurations)
        {
            entry.Configuration.Outbox!.TimeToLive = TimeSpan.FromHours(1);
            await CreateWriter(entry.Provider, entry.Configuration)();
        }
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(90))
        {
            var expiredCounts = await Task.WhenAll(configurations.Select(entry =>
                _database.GetCollection<BsonDocument>(entry.Provider)
                    .CountDocumentsAsync(new BsonDocument("_id", entry.ExpiredId))));
            if (expiredCounts.All(count => count == 0))
            {
                break;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        //Assert
        foreach (var entry in configurations)
        {
            var documents = _database.GetCollection<BsonDocument>(entry.Provider);
            await Assert.That(await documents.CountDocumentsAsync(new BsonDocument("_id", entry.ExpiredId))).IsEqualTo(0L);
            await Assert.That(await documents.CountDocumentsAsync(new BsonDocument("_id", entry.RetainedId))).IsEqualTo(1L);
            if (entry.Provider == "Locking")
            {
                var locking = new MongoDbLockingProvider(entry.Configuration);
                await Assert.That(await locking.ObtainLockAsync(entry.RetainedId.AsString, CancellationToken.None)).IsNull();
                await Assert.That(await locking.ObtainLockAsync(entry.ExpiredId.AsString, CancellationToken.None)).IsNotNull();
            }
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
                    outbox.Add(CreateMessage(configuration.TimeProvider.GetUtcNow()), null);
                    return Task.CompletedTask;
                };
            case "OutboxAsync":
                var asyncOutbox = new MongoDbOutbox(configuration);
                return () => asyncOutbox.AddAsync(CreateMessage(configuration.TimeProvider.GetUtcNow()), null);
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
                return async () => await Assert.That(await locking.ObtainLockAsync(Id.Random().Value, CancellationToken.None)).IsNotNull();
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, null);
        }
    }

    private static Message CreateMessage(DateTimeOffset timestamp)
        => new(new MessageHeader(Id.Random(), new RoutingKey("ttl-topic"), MessageType.MT_EVENT, timeStamp: timestamp),
            new MessageBody("payload"));

    private async Task AssertTtlIndexAsync()
    {
        using var cursor = await Documents.Indexes.ListAsync();
        var indexes = await cursor.ToListAsync();
        var ttlIndex = await Assert.That(System.Linq.Enumerable.Where(indexes, index => index.Contains("expireAfterSeconds"))).HasSingleItem();
        await Assert.That(ttlIndex["name"].AsString).IsEqualTo(INDEX_NAME);
        await Assert.That(ttlIndex["expireAfterSeconds"].ToInt64()).IsEqualTo(3600L);
    }

    public void Dispose() => Const.Client.DropDatabase(_databaseName);
}
