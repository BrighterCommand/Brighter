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
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.Outbox.DynamoDB;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

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

    [Test]
    [Arguments(false, 1)]
    [Arguments(true, 1)]
    [Arguments(false, 3)]
    [Arguments(true, 3)]
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
        await Assert.That(first).IsEmpty();
        await Assert.That(second).IsEmpty();
        await Assert.That(_http.Requests.Count).IsEqualTo(concurrency * 2);
        for (var segment = 0; segment < concurrency; segment++)
        {
            await Assert.That(_http.Requests.Where(request => request.Segment == segment).Select(request => request.Start))
                .IsEquivalentTo(new[] { 0, 10 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        }
    }

    [Test]
    [Arguments(false, 1)]
    [Arguments(true, 1)]
    [Arguments(false, 3)]
    [Arguments(true, 3)]
    public async Task When_healthy_messages_follow_a_tripped_backlog_should_reach_them_on_later_sweeps(bool isAsync, int concurrency)
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(scanConcurrency: concurrency), _timeProvider);
        for (var segment = 0; segment < concurrency; segment++)
        {
            _http.Topics[segment] = ["orders", "orders", "orders", "orders", "payments", "payments"];
        }

        //Act
        await Assert.That(await ReadAsync(outbox, isAsync, concurrency * 2)).IsEmpty();
        await Assert.That(await ReadAsync(outbox, isAsync, concurrency * 2)).IsEmpty();
        var healthy = await ReadAsync(outbox, isAsync, concurrency * 2);
        var reset = (await outbox.OutstandingMessagesAsync(TimeSpan.Zero, new RequestContext(), pageSize: concurrency * 2)).ToArray();

        //Assert
        await Assert.That(healthy.Length).IsEqualTo(concurrency * 2);
        foreach (var message in healthy)
        {
            await Assert.That(message.Header.Topic.Value).IsEqualTo("payments");
        }
        await Assert.That(healthy.Select(message => message.Id).Distinct().Count()).IsEqualTo(healthy.Length);
        await Assert.That(reset.Length).IsEqualTo(concurrency * 2);
        foreach (var message in reset)
        {
            await Assert.That(message.Header.Topic.Value).IsEqualTo("orders");
        }
        await Assert.That(_http.Requests.Count).IsEqualTo(concurrency * 4);
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
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
            await Assert.That(() => operation).Throws<OperationCanceledException>();
        }
        else
        {
            await Assert.That(() => operation).ThrowsExactly<InvalidOperationException>();
        }
        _http.CancelSegment = null;
        _http.FailSegment = null;
        _http.Requests.Clear();
        var retried = await ReadAsync(outbox, true, 3);

        //Assert
        await Assert.That(retried.Select(message => message.Id.Value).OrderBy(id => id))
            .IsEquivalentTo(new[] { "0-0", "1-0", "2-0" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        foreach (var request in _http.Requests)
        {
            await Assert.That(request.Start).IsEqualTo(0);
        }
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
