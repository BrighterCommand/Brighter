#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Paramore.Brighter.Extensions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Observability;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    /// <summary>
    /// A genuine end-to-end exercise of Spec 0027 (replay matching outbox events when the inbox has already seen a
    /// message). Nothing is injected through a testing seam: a command is <em>posted onto the in-memory bus through a
    /// command processor</em>, a real message pump consumes it, a handler records it in the inbox and forwards a
    /// downstream event back onto the bus, and the inbox/outbox record the receipt and the outgoing message naturally.
    /// We then post the <em>same</em> command again; the inbox recognises the duplicate, the handler does not re-run,
    /// and the outbox replays the original outgoing message back onto the bus.
    ///
    /// Coordination uses the two techniques this scenario needs: the <see cref="InternalBus"/> as the transport, and a
    /// .NET <see cref="System.Threading.Channels.Channel{T}"/> the handler writes to so the test thread can tell when
    /// the message has been processed and stop the pump deterministically.
    /// </summary>
    public sealed class EndToEndReplayOnSeenTests : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly RoutingKey _inboundRoutingKey = new("MyCommand");
        private readonly RoutingKey _outgoingRoutingKey = new("MyEvent");
        private readonly string _contextKey = typeof(ProcessAndForwardHandler).FullName!;

        private readonly InternalBus _internalBus = new();
        private readonly InMemoryInbox _inbox;
        private readonly InMemoryOutbox _outbox;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly ChannelReader<MyCommand> _handled;
        private readonly Performer _performer;
        private readonly Task _pumpTask;
        private readonly MyCommand _command;

        public EndToEndReplayOnSeenTests()
        {
            ProcessAndForwardHandler.ReceivedCount = 0;
            ProcessAndForwardHandler.OutgoingMessageId = null;

            var timeProvider = new FakeTimeProvider();
            var tracer = new BrighterTracer(timeProvider);
            _inbox = new InMemoryInbox(timeProvider);
            _outbox = new InMemoryOutbox(timeProvider) { Tracer = tracer };
            _command = new MyCommand { Value = "Replay Me" };

            //The handler signals the test thread through a .NET channel once it has processed a message
            var handledChannel = System.Threading.Channels.Channel.CreateUnbounded<MyCommand>();
            _handled = handledChannel.Reader;

            //One mapper registry serves the inbound command (posted by the test, read by the pump) and the outgoing event
            var messageMapperRegistry = new MessageMapperRegistry(
                new SimpleMessageMapperFactory(mapperType => mapperType == typeof(MyEventMessageMapper)
                    ? (IAmAMessageMapper)new MyEventMessageMapper()
                    : new MyCommandMessageMapper()),
                null);
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();
            messageMapperRegistry.Register<MyEvent, MyEventMessageMapper>();

            var resiliencePipelineRegistry = new ResiliencePipelineRegistry<string>().AddBrighterDefault();

            //Producers for both the inbound command topic and the outgoing event topic, all over the same InternalBus
            var producerRegistry = new ProducerRegistry(new Dictionary<RoutingKey, IAmAMessageProducer>
            {
                { _inboundRoutingKey, new InMemoryMessageProducer(_internalBus,
                    new Publication { Topic = _inboundRoutingKey, RequestType = typeof(MyCommand) }) },
                { _outgoingRoutingKey, new InMemoryMessageProducer(_internalBus,
                    new Publication { Topic = _outgoingRoutingKey, RequestType = typeof(MyEvent) }) }
            });

            IAmAnOutboxProducerMediator mediator = new OutboxProducerMediator<Message, CommittableTransaction>(
                producerRegistry,
                resiliencePipelineRegistry,
                messageMapperRegistry,
                new EmptyMessageTransformerFactory(),
                new EmptyMessageTransformerFactoryAsync(),
                tracer,
                new FindPublicationByPublicationTopicOrRequestType(),
                _outbox);

            //The handler needs the command processor (to forward) and the signal channel; the command processor needs the
            //handler factory. Break the cycle by resolving the processor lazily from the container the factory wraps.
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, ProcessAndForwardHandler>();

            var container = new ServiceCollection();
            container.AddTransient<ProcessAndForwardHandler>();
            container.AddSingleton(handledChannel.Writer);
            container.AddSingleton<IAmAnInboxSync>(_inbox);
            container.AddSingleton<IAmACausationTrackingOutbox>(_outbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });
            container.AddSingleton<IAmACommandProcessor>(sp => new CommandProcessor(
                registry,
                new ServiceProviderHandlerFactory(sp),
                new InMemoryRequestContextFactory(),
                new DefaultPolicy(),
                resiliencePipelineRegistry,
                mediator,
                new InMemorySchedulerFactory()));

            _commandProcessor = container.BuildServiceProvider().GetRequiredService<IAmACommandProcessor>();

            //A real pump consuming the inbound topic off the bus, hosted on a background thread via a Performer
            var channel = new Channel(
                new ChannelName("MyChannel"),
                _inboundRoutingKey,
                new InMemoryMessageConsumer(_inboundRoutingKey, _internalBus, timeProvider, ackTimeout: TimeSpan.FromMilliseconds(1000)));

            var pump = new Reactor(_commandProcessor, _ => typeof(MyCommand),
                    messageMapperRegistry, new EmptyMessageTransformerFactory(), new InMemoryRequestContextFactory(), channel)
                { Channel = channel, TimeOut = TimeSpan.FromMilliseconds(200), EmptyChannelDelay = TimeSpan.FromMilliseconds(10) };

            _performer = new Performer(channel, pump);
            _pumpTask = _performer.Run();
        }

        [Fact]
        public async Task When_a_seen_message_is_replayed_end_to_end_through_the_internal_bus()
        {
            // --- New message: post it onto the bus through the command processor ---
            _commandProcessor.Post(_command, new RequestContext());

            var processed = await WaitForHandlerSignal();
            Assert.Equal(_command.Id, processed.Id); //the command travelled over the bus, so it is a deserialized copy
            Assert.Equal(1, ProcessAndForwardHandler.ReceivedCount);

            // The inbox recorded receipt of the command, stamped with its own id as the causation id
            var inboxCausationId = ((IAmACausationTrackingInbox)_inbox)
                .GetCausationId(_command.Id, _contextKey, new RequestContext());
            Assert.Equal(_command.Id.Value, inboxCausationId);

            // The handler forwarded a downstream event; it is on the bus and recorded in the outbox
            var outgoingMessageId = ProcessAndForwardHandler.OutgoingMessageId!;
            var onBus = _internalBus.Stream(_outgoingRoutingKey).ToArray();
            Assert.Single(onBus);
            Assert.Equal(outgoingMessageId.Value, onBus[0].Id.Value);

            // --- Replay: post the SAME command again ---
            _commandProcessor.Post(_command, new RequestContext());

            using var cts = new CancellationTokenSource(Timeout);
            await WaitForMessageToBecomeOutstanding(outgoingMessageId, cts.Token);
            Assert.Equal(1, ProcessAndForwardHandler.ReceivedCount);

            // Re-dispatch the replayed message to the bus with the same primitive Post uses (no background sweeper needed)
            _commandProcessor.ClearOutbox([outgoingMessageId], new RequestContext());

            var afterReplay = _internalBus.Stream(_outgoingRoutingKey).ToArray();
            Assert.Equal(2, afterReplay.Length);
            Assert.All(afterReplay, m => Assert.Equal(outgoingMessageId.Value, m.Id.Value));
        }

        private async Task<MyCommand> WaitForHandlerSignal()
        {
            using var cts = new CancellationTokenSource(Timeout);
            try
            {
                return await _handled.ReadAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new Xunit.Sdk.XunitException("Timed out waiting for the handler to process the message off the bus.");
            }
        }

        /// <summary>
        /// Polls the outbox until the given message is outstanding again, which is how we observe a duplicate being
        /// replayed end-to-end. The handler does not run on a duplicate, so it cannot signal the test thread the way it
        /// does for a new message; instead we watch for the effect of the replay on the outbox.
        /// </summary>
        /// <remarks>
        /// The forwarded message is dispatched (<c>Post</c> sends it immediately), so it starts off <em>not</em>
        /// outstanding. When the duplicate is recognised, <c>UseInboxHandler</c> calls
        /// <see cref="IAmACausationTrackingOutbox.ReplayCausation"/> with the <em>inbox's</em> causation id, which clears
        /// the dispatched state of every outbox entry stored under that id, flipping them back to outstanding. Because
        /// the replay is keyed on the inbox's causation id, the forwarded message only reappears here if the outbox
        /// stored it under the same causation id as the inbox entry — so this wait is also the proof of that link.
        /// </remarks>
        /// <param name="messageId">The id of the forwarded outbox message we expect the replay to make outstanding.</param>
        /// <param name="cancellationToken">A token, typically backed by a timeout, that breaks the poll loop so a failure
        /// to replay surfaces as a test failure rather than hanging forever.</param>
        private async Task WaitForMessageToBecomeOutstanding(Id messageId, CancellationToken cancellationToken)
        {
            while (!_outbox.OutstandingMessages(TimeSpan.Zero, new RequestContext()).Any(m => m.Id.Value == messageId.Value))
            {
                //the token is timeout-backed, so this is how the poll loop gives up rather than spinning forever
                if (cancellationToken.IsCancellationRequested)
                    throw new Xunit.Sdk.XunitException("Timed out waiting for the duplicate to replay the outgoing message.");

                await Task.Delay(10);
            }
        }

        public void Dispose()
        {
            _performer.Stop(_inboundRoutingKey);
            _pumpTask.Wait(Timeout);
            _performer.Dispose();
        }
    }
}
