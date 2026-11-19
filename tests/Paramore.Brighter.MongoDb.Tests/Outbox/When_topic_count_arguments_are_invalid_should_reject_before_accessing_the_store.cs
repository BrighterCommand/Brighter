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

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Paramore.Brighter.MongoDb;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

public class MongoDbTopicCountValidationTests : IDisposable
{
    private readonly MongoClient _client;
    private readonly IAmATopicCountingOutbox _outbox;
    private readonly ConcurrentQueue<CommandStartedEvent> _commands = new();

    public MongoDbTopicCountValidationTests()
    {
        var settings = MongoClientSettings.FromConnectionString(Const.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromMilliseconds(100);
        settings.ClusterConfigurator = builder => builder.Subscribe<CommandStartedEvent>(_commands.Enqueue);
        _client = new MongoClient(settings);
        _outbox = new MongoDbOutbox(new MongoDbConfiguration(_client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = $"MissingTopicCountStore{Guid.NewGuid():N}",
                MakeCollection = OnResolvingACollection.Validate
            }
        });
    }

    [Theory]
    [InlineData("null-map", typeof(ArgumentNullException), "dispatchedSinceByTopic")]
    [InlineData("empty-topic", typeof(ArgumentException), "dispatchedSinceByTopic")]
    [InlineData("zero-age", typeof(ArgumentException), "dispatchedSinceByTopic")]
    [InlineData("negative-age", typeof(ArgumentException), "dispatchedSinceByTopic")]
    [InlineData("too-old", typeof(ArgumentException), "dispatchedSinceByTopic")]
    [InlineData("zero-ceiling", typeof(ArgumentOutOfRangeException), "maxCountPerTopic")]
    [InlineData("negative-ceiling", typeof(ArgumentOutOfRangeException), "maxCountPerTopic")]
    public async Task When_topic_count_arguments_are_invalid_should_reject_before_accessing_the_store(
        string invalidArgument, Type exceptionType, string parameterName)
    {
        //Arrange
        var request = new Dictionary<RoutingKey, TimeSpan> { [new RoutingKey("first-valid-topic")] = TimeSpan.FromMinutes(5) };
        var maxCount = 100;
        switch (invalidArgument)
        {
            case "empty-topic": request[RoutingKey.Empty] = TimeSpan.FromMinutes(5); break;
            case "zero-age": request[new RoutingKey("invalid")] = TimeSpan.Zero; break;
            case "negative-age": request[new RoutingKey("invalid")] = TimeSpan.FromTicks(-1); break;
            case "too-old": request[new RoutingKey("invalid")] = TimeSpan.FromDays(365).Add(TimeSpan.FromTicks(1)); break;
            case "zero-ceiling": maxCount = 0; break;
            case "negative-ceiling": maxCount = -1; break;
        }

        //Act
        var exception = await Assert.ThrowsAsync(exceptionType, () => _outbox.GetOutstandingMessageCountsByTopicAsync(
            invalidArgument == "null-map" ? null! : request, null, maxCount));

        //Assert
        Assert.Equal(parameterName, ((ArgumentException)exception).ParamName);
        Assert.Empty(_commands);
    }

    [Fact]
    public async Task When_no_topics_are_requested_should_not_access_the_store()
    {
        //Arrange
        var request = new Dictionary<RoutingKey, TimeSpan>();

        //Act
        var counts = await _outbox.GetOutstandingMessageCountsByTopicAsync(request, null, 100);

        //Assert
        Assert.Empty(counts);
        Assert.Empty(_commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_topic_count_is_already_cancelled_should_not_access_the_store(bool emptyRequest)
    {
        //Arrange
        var request = new Dictionary<RoutingKey, TimeSpan>();
        if (!emptyRequest)
            request[new RoutingKey("orders")] = TimeSpan.FromMinutes(5);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        //Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _outbox.GetOutstandingMessageCountsByTopicAsync(
            request, null, 100, cancellationToken: cancellation.Token));

        //Assert
        Assert.Empty(_commands);
    }

    public void Dispose() => _client.Dispose();
}
