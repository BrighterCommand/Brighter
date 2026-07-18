#region Licence

/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Clear;

public class IndependentOutboxSweepTests
{
    private readonly ControlledAsyncOutbox _firstOutbox = new(waitForRelease: true);
    private readonly ControlledAsyncOutbox _secondOutbox = new(waitForRelease: false);
    private readonly OutboxProducerMediator<Message, IndependentOutboxSweepTransaction> _firstMediator;
    private readonly OutboxProducerMediator<Message, IndependentOutboxSweepTransaction> _secondMediator;

    public IndependentOutboxSweepTests()
    {
        _firstMediator = CreateMediator(_firstOutbox);
        _secondMediator = CreateMediator(_secondOutbox);
    }

    [Fact]
    public async Task When_independent_mediators_sweep_concurrently_should_clear_both_outboxes()
    {
        // Arrange
        Task firstSweep = _firstMediator.ClearOutstandingFromOutboxAsync(
            amountToClear: 100,
            minimumAge: TimeSpan.Zero,
            useBulk: false,
            requestContext: new RequestContext());

        try
        {
            await _firstOutbox.WaitForSweepAsync();

            // Act
            await _secondMediator.ClearOutstandingFromOutboxAsync(
                amountToClear: 100,
                minimumAge: TimeSpan.Zero,
                useBulk: false,
                requestContext: new RequestContext());
        }
        finally
        {
            _firstOutbox.ReleaseSweep();
            await firstSweep;
        }

        // Assert
        Assert.Equal(1, _firstOutbox.SweepCount);
        Assert.Equal(1, _secondOutbox.SweepCount);
    }

    private static OutboxProducerMediator<Message, IndependentOutboxSweepTransaction> CreateMediator(
        IAmAnOutboxAsync<Message, IndependentOutboxSweepTransaction> outbox)
    {
        var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>());
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => throw new InvalidOperationException("No mapper should be requested.")),
            new SimpleMessageMapperFactoryAsync(_ => throw new InvalidOperationException("No mapper should be requested.")));

        return new OutboxProducerMediator<Message, IndependentOutboxSweepTransaction>(
            producerRegistry,
            new ResiliencePipelineRegistry<string>().AddBrighterDefault(),
            mapperRegistry,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            outbox);
    }

    private sealed class IndependentOutboxSweepTransaction
    {
    }

    private sealed class ControlledAsyncOutbox(bool waitForRelease)
        : IAmAnOutboxAsync<Message, IndependentOutboxSweepTransaction>
    {
        private readonly TaskCompletionSource<bool> _sweepStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _sweepRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _sweepCount;

        public int SweepCount => Volatile.Read(ref _sweepCount);

        public IAmABrighterTracer? Tracer { private get; set; }

        public bool ContinueOnCapturedContext { get; set; }

        public async Task WaitForSweepAsync()
        {
            await _sweepStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void ReleaseSweep() => _sweepRelease.TrySetResult(true);

        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
            TimeSpan dispatchedSince,
            RequestContext requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            IEnumerable<RoutingKey>? trippedTopics = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _sweepCount);
            _sweepStarted.TrySetResult(true);

            if (waitForRelease)
                await _sweepRelease.Task.WaitAsync(cancellationToken);

            return [];
        }

        public Task AddAsync(
            Message message,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<IndependentOutboxSweepTransaction>? transactionProvider = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddAsync(
            IEnumerable<Message> messages,
            RequestContext? requestContext,
            int outBoxTimeout = -1,
            IAmABoxTransactionProvider<IndependentOutboxSweepTransaction>? transactionProvider = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(
            Id[] messageIds,
            RequestContext requestContext,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<Message>> DispatchedMessagesAsync(
            TimeSpan dispatchedSince,
            RequestContext requestContext,
            int pageSize = 100,
            int pageNumber = 1,
            int outboxTimeout = -1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Message> GetAsync(
            Id messageId,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<Message>> GetAsync(
            IEnumerable<Id> messageIds,
            RequestContext requestContext,
            int outBoxTimeout = -1,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MarkDispatchedAsync(
            Id id,
            RequestContext requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MarkDispatchedAsync(
            IEnumerable<Id> ids,
            RequestContext requestContext,
            DateTimeOffset? dispatchedAt = null,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<int> GetOutstandingMessageCountAsync(
            TimeSpan dispatchedSince,
            RequestContext? requestContext,
            int maxCount = 100,
            Dictionary<string, object>? args = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
