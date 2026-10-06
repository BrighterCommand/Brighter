#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.DynamoDB.V4.Tests.TestDoubles;
using Paramore.Brighter.Outbox.DynamoDB.V4;
using Xunit;

namespace Paramore.Brighter.DynamoDB.V4.Tests.Outbox;

public class DynamoDbTrippedTopicScanProgressTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly InMemoryDynamoDbScanHttpHandler _http;
    private readonly AmazonDynamoDBClient _client;

    public DynamoDbTrippedTopicScanProgressTests()
    {
        _http = new InMemoryDynamoDbScanHttpHandler(_timeProvider);
        _client = new AmazonDynamoDBClient(new BasicAWSCredentials("test-access-key", "test-secret-key"),
            new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost",
                AuthenticationRegion = "us-east-1",
                HttpClientFactory = new InMemoryDynamoDbScanHttpClientFactory(_http),
                MaxErrorRetry = 0
            });
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public async Task When_a_large_topic_is_tripped_should_bound_each_sweep_and_resume(bool isAsync, int concurrency)
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(scanConcurrency: concurrency), _timeProvider);
        for (var segment = 0; segment < concurrency; segment++)
        {
            _http.Topics[segment] = Enumerable.Repeat("orders", 100_000).Append("payments").ToArray();
        }

        //Act
        var first = await ReadAsync(outbox, isAsync, concurrency * 10);
        var second = await ReadAsync(outbox, isAsync, concurrency * 10);

        //Assert
        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Equal(concurrency * 2, _http.Requests.Count);
        for (var segment = 0; segment < concurrency; segment++)
        {
            Assert.Equal(new[] { 0, 10 }, _http.Requests.Where(request => request.Segment == segment).Select(request => request.Start));
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    public async Task When_healthy_messages_follow_a_tripped_backlog_should_reach_them_on_later_sweeps(bool isAsync, int concurrency)
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(scanConcurrency: concurrency), _timeProvider);
        for (var segment = 0; segment < concurrency; segment++)
        {
            _http.Topics[segment] = ["orders", "orders", "orders", "orders", "payments", "payments"];
        }

        //Act
        Assert.Empty(await ReadAsync(outbox, isAsync, concurrency * 2));
        Assert.Empty(await ReadAsync(outbox, isAsync, concurrency * 2));
        var healthy = await ReadAsync(outbox, isAsync, concurrency * 2);
        var reset = (await outbox.OutstandingMessagesAsync(TimeSpan.Zero, new RequestContext(), pageSize: concurrency * 2)).ToArray();

        //Assert
        Assert.Equal(concurrency * 2, healthy.Length);
        Assert.All(healthy, message => Assert.Equal("payments", message.Header.Topic.Value));
        Assert.Equal(healthy.Length, healthy.Select(message => message.Id).Distinct().Count());
        Assert.Equal(concurrency * 2, reset.Length);
        Assert.All(reset, message => Assert.Equal("orders", message.Header.Topic.Value));
        Assert.Equal(concurrency * 4, _http.Requests.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_one_segment_fails_should_not_skip_other_segments_messages_on_retry(bool cancel)
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(timeout: 10000, scanConcurrency: 3), _timeProvider);
        for (var segment = 0; segment < 3; segment++)
        {
            _http.Topics[segment] = ["payments", "orders", "payments", "orders"];
        }
        _http.CancelSegment = cancel ? 2 : null;
        _http.FailSegment = cancel ? null : 2;

        //Act
        var operation = ReadAsync(outbox, true, 3);
        if (cancel)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        }
        _http.CancelSegment = null;
        _http.FailSegment = null;
        _http.Requests.Clear();
        var retried = await ReadAsync(outbox, true, 3);

        //Assert
        Assert.Equal(new[] { "0-0", "1-0", "2-0" }, retried.Select(message => message.Id.Value).OrderBy(id => id));
        Assert.All(_http.Requests, request => Assert.Equal(0, request.Start));
    }

    private static async Task<Message[]> ReadAsync(DynamoDbOutbox outbox, bool isAsync, int pageSize)
        => (isAsync
            ? await outbox.OutstandingMessagesAsync(TimeSpan.Zero, new RequestContext(), pageSize: pageSize, trippedTopics: [new RoutingKey("orders")])
            : outbox.OutstandingMessages(TimeSpan.Zero, new RequestContext(), pageSize: pageSize, trippedTopics: [new RoutingKey("orders")])).ToArray();

    public void Dispose()
    {
        _client.Dispose();
        _http.Dispose();
    }
}
