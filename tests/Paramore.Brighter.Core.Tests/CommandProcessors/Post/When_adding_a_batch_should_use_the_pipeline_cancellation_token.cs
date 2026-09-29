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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Extensions;
using Polly;
using Polly.Registry;


namespace Paramore.Brighter.Core.Tests.CommandProcessors.Post;

public class AsyncOutboxResilienceCancellationTests : IDisposable
{
    private readonly InternalBus _bus = new();
    private readonly RoutingKey _topic = new("outbox-cancellation");
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly InMemoryCancellationOutbox _outbox;
    private readonly InMemoryCancellationMessageProducer _producer;
    private readonly OutboxProducerMediator<Message, CommittableTransaction> _mediator;
    private readonly ResiliencePipelineRegistry<string> _pipelines = new();
    private readonly MessageMapperRegistry _mappers = new(
        new SimpleMessageMapperFactory(_ => throw new InvalidOperationException("No mapper should be requested.")),
        null);
    private readonly CancellationTokenSource _callerCancellation = new();
    private readonly CancellationTokenSource _resilienceCancellation = new();
    private readonly ResilienceContext _resilienceContext;

    public AsyncOutboxResilienceCancellationTests()
    {
        _outbox = new InMemoryCancellationOutbox(_timeProvider);
        _producer = new InMemoryCancellationMessageProducer(_bus, new Publication { Topic = _topic });
        _resilienceContext = ResilienceContextPool.Shared.Get(_resilienceCancellation.Token);
        _pipelines.AddBrighterDefault();
        _mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
            new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer> { { _topic, _producer } }),
            _pipelines,
            _mappers,
            new EmptyMessageTransformerFactory(),
            new EmptyMessageTransformerFactoryAsync(),
            tracer: null,
            new FindPublicationByPublicationTopicOrRequestType(),
            _outbox,
            maxOutStandingCheckInterval: TimeSpan.FromMinutes(1),
            timeProvider: _timeProvider);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task When_adding_a_batch_should_use_the_pipeline_cancellation_token(bool useResilienceContext)
    {
        // Arrange
        var context = CreateContext(useResilienceContext);
        var message = CreateMessage();
        var secondMessage = CreateMessage();
        var batchId = _mediator.StartBatchAddToOutbox();
        await _mediator.AddToOutboxAsync(message, context, batchId: batchId);
        await _mediator.AddToOutboxAsync(secondMessage, context, batchId: batchId);

        // Act
        await _mediator.EndBatchAddToOutboxAsync(batchId, null, context, _callerCancellation.Token);

        // Assert
        await Assert.That((await Assert.That(_outbox.AddTokens).HasSingleItem())).IsEqualTo(ExpectedToken(useResilienceContext));
        await Assert.That(_outbox.Get(message.Id, context).Id).IsEqualTo(message.Id);
        await Assert.That(_outbox.Get(secondMessage.Id, context).Id).IsEqualTo(secondMessage.Id);
        await Assert.That(_bus.Stream(_topic)).IsEmpty();
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(false, false)]
    [Arguments(true, false)]
    public async Task When_sending_messages_should_use_the_pipeline_cancellation_token(
        bool useBulk, bool useResilienceContext)
    {
        // Arrange
        var context = CreateContext(useResilienceContext);
        var message = CreateMessage();
        _outbox.Add(message, context);

        // Act
        await DispatchAsync(message, context, useBulk);

        // Assert
        await Assert.That((await Assert.That(_producer.SendTokens).HasSingleItem())).IsEqualTo(ExpectedToken(useResilienceContext));
        await Assert.That((await Assert.That(_bus.Stream(_topic)).HasSingleItem()).Id).IsEqualTo(message.Id);
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(false, false)]
    [Arguments(true, false)]
    public async Task When_marking_messages_dispatched_should_use_the_pipeline_cancellation_token(
        bool useBulk, bool useResilienceContext)
    {
        // Arrange
        var context = CreateContext(useResilienceContext);
        var message = CreateMessage();
        Message[] messages = [message, CreateMessage()];
        _outbox.Add(messages, context);

        // Act
        if (useBulk)
        {
            await _mediator.ClearOutstandingFromOutboxAsync(2, TimeSpan.Zero, true, context,
                cancellationToken: _callerCancellation.Token);
        }
        else
        {
            await _mediator.ClearOutboxAsync([messages[0].Id, messages[1].Id], context,
                cancellationToken: _callerCancellation.Token);
        }

        // Assert
        await Assert.That(_outbox.MarkDispatchedTokens.Count).IsEqualTo(2);
        using (Assert.Multiple())
        {
            foreach (var token in _outbox.MarkDispatchedTokens)
            {
                await Assert.That(token).IsEqualTo(ExpectedToken(useResilienceContext));
            }
        }
        await Assert.That(_outbox.OutstandingMessages(TimeSpan.Zero, context)).IsEmpty();
        var sentMessages = _bus.Stream(_topic).ToArray();
        await Assert.That(sentMessages.Length).IsEqualTo(messages.Length);
        foreach (var expected in messages)
{
    await Assert.That(sentMessages).Contains(sent => sent.Id == expected.Id);
}
    }

    [Test]
    public async Task When_cancelling_during_a_batch_add_should_leave_the_batch_unstored()
    {
        // Arrange
        var context = CreateContext(true);
        var message = CreateMessage();
        var batchId = _mediator.StartBatchAddToOutbox();
        await _mediator.AddToOutboxAsync(message, context, batchId: batchId);
        _outbox.OnAdding = _resilienceCancellation.Cancel;

        // Act
        await Assert.That(() =>
            _mediator.EndBatchAddToOutboxAsync(batchId, null, context, _callerCancellation.Token)).ThrowsExactly<ChannelFailureException>();

        // Assert
        await Assert.That(_outbox.AddTokens).HasSingleItem();
        await Assert.That(_outbox.OutstandingMessages(TimeSpan.Zero, context)).IsEmpty();
        await Assert.That(_callerCancellation.IsCancellationRequested).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_cancelling_during_send_should_leave_the_message_outstanding(bool useBulk)
    {
        // Arrange
        var context = CreateContext(true);
        var message = CreateMessage();
        _outbox.Add(message, context);
        _producer.OnSending = _resilienceCancellation.Cancel;

        // Act
        await DispatchAsync(message, context, useBulk);

        // Assert
        await Assert.That(_producer.SendTokens).HasSingleItem();
        await Assert.That(_bus.Stream(_topic)).IsEmpty();
        await Assert.That(_outbox.MarkDispatchedTokens).IsEmpty();
        await Assert.That((await Assert.That(_outbox.OutstandingMessages(TimeSpan.Zero, context)).HasSingleItem()).Id).IsEqualTo(message.Id);
        await Assert.That(_callerCancellation.IsCancellationRequested).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_cancelling_during_dispatch_marking_should_leave_the_message_outstanding(bool useBulk)
    {
        // Arrange
        var context = CreateContext(true);
        var message = CreateMessage();
        _outbox.Add(message, context);
        _outbox.OnMarkingDispatched = _resilienceCancellation.Cancel;

        // Act
        await DispatchAsync(message, context, useBulk);

        // Assert
        await Assert.That(_outbox.MarkDispatchedTokens).HasSingleItem();
        await Assert.That((await Assert.That(_bus.Stream(_topic)).HasSingleItem()).Id).IsEqualTo(message.Id);
        await Assert.That((await Assert.That(_outbox.OutstandingMessages(TimeSpan.Zero, context)).HasSingleItem()).Id).IsEqualTo(message.Id);
        await Assert.That(_callerCancellation.IsCancellationRequested).IsFalse();
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task When_a_batch_add_times_out_should_leave_the_batch_unstored(bool useResilienceContext)
    {
        // Arrange
        ConfigureTimeout();
        var context = CreateContext(useResilienceContext);
        var message = CreateMessage();
        var batchId = _mediator.StartBatchAddToOutbox();
        await _mediator.AddToOutboxAsync(message, context, batchId: batchId);
        _outbox.OnAdding = () => _timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Act
        await Assert.That(() =>
            _mediator.EndBatchAddToOutboxAsync(batchId, null, context, _callerCancellation.Token)).ThrowsExactly<ChannelFailureException>();

        // Assert
        await Assert.That((await Assert.That(_outbox.AddTokens).HasSingleItem()).IsCancellationRequested).IsTrue();
        await Assert.That(_outbox.OutstandingMessages(TimeSpan.Zero, context)).IsEmpty();
        await Assert.That(_callerCancellation.IsCancellationRequested).IsFalse();
        await Assert.That(_resilienceCancellation.IsCancellationRequested).IsFalse();
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(false, false)]
    [Arguments(true, false)]
    public async Task When_a_send_times_out_should_leave_the_message_outstanding(
        bool useBulk, bool useResilienceContext)
    {
        // Arrange
        ConfigureTimeout();
        var context = CreateContext(useResilienceContext);
        var message = CreateMessage();
        _outbox.Add(message, context);
        _producer.OnSending = () => _timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Act
        await DispatchAsync(message, context, useBulk);

        // Assert
        await Assert.That((await Assert.That(_producer.SendTokens).HasSingleItem()).IsCancellationRequested).IsTrue();
        await Assert.That(_bus.Stream(_topic)).IsEmpty();
        await Assert.That(_outbox.MarkDispatchedTokens).IsEmpty();
        await Assert.That((await Assert.That(_outbox.OutstandingMessages(TimeSpan.Zero, context)).HasSingleItem()).Id).IsEqualTo(message.Id);
        await Assert.That(_callerCancellation.IsCancellationRequested).IsFalse();
        await Assert.That(_resilienceCancellation.IsCancellationRequested).IsFalse();
    }

    [Test]
    [Arguments(false, true)]
    [Arguments(true, true)]
    [Arguments(false, false)]
    [Arguments(true, false)]
    public async Task When_dispatch_marking_times_out_should_leave_the_message_outstanding(
        bool useBulk, bool useResilienceContext)
    {
        // Arrange
        ConfigureTimeout();
        var context = CreateContext(useResilienceContext);
        var message = CreateMessage();
        _outbox.Add(message, context);
        _outbox.OnMarkingDispatched = () => _timeProvider.Advance(TimeSpan.FromSeconds(2));

        // Act
        await DispatchAsync(message, context, useBulk);

        // Assert
        await Assert.That((await Assert.That(_outbox.MarkDispatchedTokens).HasSingleItem()).IsCancellationRequested).IsTrue();
        await Assert.That((await Assert.That(_bus.Stream(_topic)).HasSingleItem()).Id).IsEqualTo(message.Id);
        await Assert.That((await Assert.That(_outbox.OutstandingMessages(TimeSpan.Zero, context)).HasSingleItem()).Id).IsEqualTo(message.Id);
        await Assert.That(_callerCancellation.IsCancellationRequested).IsFalse();
        await Assert.That(_resilienceCancellation.IsCancellationRequested).IsFalse();
    }

    private void ConfigureTimeout()
        => _pipelines.GetOrAddPipeline(CommandProcessor.OutboxProducer, builder =>
        {
            builder.TimeProvider = _timeProvider;
            builder.AddTimeout(TimeSpan.FromSeconds(1));
        });

    private RequestContext CreateContext(bool useResilienceContext)
        => new() { ResilienceContext = useResilienceContext ? _resilienceContext : null };

    private CancellationToken ExpectedToken(bool useResilienceContext)
        => useResilienceContext ? _resilienceCancellation.Token : _callerCancellation.Token;

    private Message CreateMessage()
        => new(new MessageHeader(Id.Random(), _topic, MessageType.MT_COMMAND), new MessageBody("test message"));

    private Task DispatchAsync(Message message, RequestContext context, bool useBulk)
        => useBulk
            ? _mediator.ClearOutstandingFromOutboxAsync(1, TimeSpan.Zero, true, context,
                cancellationToken: _callerCancellation.Token)
            : _mediator.ClearOutboxAsync([message.Id], context, cancellationToken: _callerCancellation.Token);

    public void Dispose()
    {
        _mediator.Dispose();
        _mappers.Dispose();
        _pipelines.Dispose();
        ResilienceContextPool.Shared.Return(_resilienceContext);
        _resilienceCancellation.Dispose();
        _callerCancellation.Dispose();
    }
}
