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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.DynamoDB.Tests.TestDoubles;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Outbox.DynamoDB;
using Xunit;

namespace Paramore.Brighter.DynamoDB.Tests.Outbox;

public class DynamoDbOutboxTimeoutTests : IDisposable
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly DelayedDynamoDbHttpHandler _http = new();
    private readonly AmazonDynamoDBClient _client;

    public DynamoDbOutboxTimeoutTests()
    {
        _client = new AmazonDynamoDBClient(new BasicAWSCredentials("test-access-key", "test-secret-key"),
            new AmazonDynamoDBConfig
            {
                ServiceURL = "http://localhost",
                AuthenticationRegion = "us-east-1",
                HttpClientFactory = new DelayedDynamoDbHttpClientFactory(_http),
                MaxErrorRetry = 0
            });
    }

    public static IEnumerable<object[]> TimeoutCases()
    {
        foreach (var operation in new[] { "Get", "GetBatch", "Add", "AddBatch", "Dispatched", "DispatchedTopic" })
        foreach (var isAsync in new[] { false, true })
        {
            yield return [operation, isAsync, 100, -1];
            yield return [operation, isAsync, 10_000, 100];
            yield return [operation, isAsync, 100, 10_000];
        }
    }

    [Theory]
    [MemberData(nameof(TimeoutCases))]
    public async Task When_an_outbox_timeout_expires_should_cancel_the_pending_operation(
        string name, bool isAsync, int configuredTimeout, int outboxTimeout)
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(timeout: configuredTimeout), _timeProvider);
        var expectedTimeout = outboxTimeout == -1 ? configuredTimeout : outboxTimeout;

        //Act
        var operation = Task.Run(() => Execute(outbox, name, isAsync, outboxTimeout));
        try
        {
            var token = await AwaitOperation(operation);

            //Assert
            _timeProvider.Advance(TimeSpan.FromMilliseconds(expectedTimeout - 1));
            Assert.False(token.IsCancellationRequested);
            _timeProvider.Advance(TimeSpan.FromMilliseconds(1));
            Assert.True(token.IsCancellationRequested);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            _http.CompleteOperation();
            await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData("Delete")]
    [InlineData("MarkDispatched")]
    [InlineData("MarkDispatchedBatch")]
    [InlineData("Outstanding")]
    [InlineData("OutstandingTopic")]
    [InlineData("Count")]
    public async Task When_no_per_call_timeout_is_available_should_use_the_configured_timeout(string name)
    {
        await When_an_outbox_timeout_expires_should_cancel_the_pending_operation(name, true, 100, -1);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, -1)]
    public async Task When_the_timeout_is_disabled_should_allow_completion(int configuredTimeout, int outboxTimeout)
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(timeout: configuredTimeout), _timeProvider);

        //Act
        var operation = Task.Run(() => outbox.GetAsync(Id.Random(), new RequestContext(), outboxTimeout));
        try
        {
            var token = await AwaitOperation(operation);
            _timeProvider.Advance(TimeSpan.FromDays(1));

            //Assert
            Assert.False(token.IsCancellationRequested);
            Assert.False(operation.IsCompleted);
            _http.CompleteOperation();
            var message = await operation.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(message.IsEmpty);
        }
        finally
        {
            _http.CompleteOperation();
            await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task When_the_caller_cancels_should_cancel_even_with_a_disabled_timeout(int timeout)
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(timeout: timeout), _timeProvider);
        using var cancellation = new CancellationTokenSource();

        //Act
        var operation = Task.Run(() => outbox.GetAsync(Id.Random(), new RequestContext(), cancellationToken: cancellation.Token));
        try
        {
            await AwaitOperation(operation);
            cancellation.Cancel();

            //Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            _http.CompleteOperation();
            await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
        }
    }

    [Fact]
    public async Task When_completing_before_the_timeout_should_preserve_the_callers_token()
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(timeout: 100), _timeProvider);
        using var cancellation = new CancellationTokenSource();
        _http.CompleteOperation();

        //Act
        await outbox.GetAsync(Id.Random(), new RequestContext(), cancellationToken: cancellation.Token);
        _timeProvider.Advance(TimeSpan.FromMilliseconds(100));

        //Assert
        Assert.False(cancellation.IsCancellationRequested);
        await outbox.GetAsync(Id.Random(), new RequestContext(), cancellationToken: cancellation.Token);
    }

    [Fact]
    public async Task When_the_caller_has_already_cancelled_should_not_send_a_request()
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(), _timeProvider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        //Act
        var operation = outbox.AddAsync(new DefaultMessageBuilder().Build(), new RequestContext(), cancellationToken: cancellation.Token);

        //Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.False(_http.OperationStarted.IsCompleted);
    }

    [Fact]
    public async Task When_a_timeout_is_invalid_should_reject_it_before_io()
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(), _timeProvider);

        //Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new DynamoDbConfiguration(timeout: -2));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => outbox.GetAsync(Id.Random(), new RequestContext(), -2));
        Assert.False(_http.OperationStarted.IsCompleted);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_a_batch_uses_its_timeout_should_not_restart_the_deadline_for_each_message(bool isAsync)
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(timeout: 100), _timeProvider);
        _http.CompleteOperation();
        IEnumerable<Message> Messages()
        {
            yield return new DefaultMessageBuilder().Build();
            _timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            yield return new DefaultMessageBuilder().Build();
        }

        //Act
        var operation = Task.Run(async () =>
        {
            if (isAsync)
                await outbox.AddAsync(Messages(), new RequestContext(), 200);
            else
                outbox.Add(Messages(), new RequestContext(), 200);
        });

        //Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(1, _http.RequestCount);
    }

    [Fact]
    public async Task When_adding_to_a_transaction_should_allow_commit_after_the_add_timeout()
    {
        //Arrange
        var outbox = new DynamoDbOutbox(_client, new DynamoDbConfiguration(timeout: 100), _timeProvider);
        using var transaction = new DynamoDbUnitOfWork(_client);
        _http.CompleteOperation();

        //Act
        await outbox.AddAsync(new DefaultMessageBuilder().Build(), new RequestContext(), transactionProvider: transaction);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        //Assert
        Assert.Single(transaction.GetTransaction().TransactItems);
        Assert.Equal(0, _http.RequestCount);
        await transaction.CommitAsync();
        Assert.Equal(1, _http.RequestCount);
    }

    private async Task<CancellationToken> AwaitOperation(Task operation)
    {
        await Task.WhenAny(_http.OperationStarted, operation).WaitAsync(TimeSpan.FromSeconds(10));
        if (operation.IsCompleted)
            await operation;
        return await _http.OperationStarted.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task Execute(DynamoDbOutbox outbox, string name, bool isAsync, int timeout)
    {
        var message = new DefaultMessageBuilder().Build();
        var context = new RequestContext();
        var topic = new Dictionary<string, object> { ["Topic"] = "timeout-test" };
        if (isAsync)
        {
            switch (name)
            {
                case "Get": await outbox.GetAsync(message.Id, context, timeout); break;
                case "GetBatch": await outbox.GetAsync(new[] { message.Id }, context, timeout); break;
                case "Add": await outbox.AddAsync(message, context, timeout); break;
                case "AddBatch": await outbox.AddAsync(new[] { message, new DefaultMessageBuilder().Build() }, context, timeout); break;
                case "Dispatched": await outbox.DispatchedMessagesAsync(TimeSpan.Zero, context, outboxTimeout: timeout); break;
                case "DispatchedTopic": await outbox.DispatchedMessagesAsync(TimeSpan.Zero, context, outboxTimeout: timeout, args: topic); break;
                case "Delete": await outbox.DeleteAsync(new[] { message.Id }, context); break;
                case "MarkDispatched": await outbox.MarkDispatchedAsync(message.Id, context); break;
                case "MarkDispatchedBatch": await outbox.MarkDispatchedAsync(new[] { message.Id }, context); break;
                case "Outstanding": await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context); break;
                case "OutstandingTopic": await outbox.OutstandingMessagesAsync(TimeSpan.Zero, context, args: topic); break;
                case "Count": await outbox.GetOutstandingMessageCountAsync(TimeSpan.Zero, context); break;
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }
        }
        else
        {
            switch (name)
            {
                case "Get": outbox.Get(message.Id, context, timeout); break;
                case "GetBatch": outbox.Get(new[] { message.Id }, context, timeout); break;
                case "Add": outbox.Add(message, context, timeout); break;
                case "AddBatch": outbox.Add(new[] { message, new DefaultMessageBuilder().Build() }, context, timeout); break;
                case "Dispatched": outbox.DispatchedMessages(TimeSpan.Zero, context, outboxTimeout: timeout); break;
                case "DispatchedTopic": outbox.DispatchedMessages(TimeSpan.Zero, context, outboxTimeout: timeout, args: topic); break;
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }
        }
    }

    public void Dispose()
    {
        _http.CompleteOperation();
        _client.Dispose();
        _http.Dispose();
    }
}
