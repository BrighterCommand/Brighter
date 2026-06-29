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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Paramore.Brighter.Observability;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class UseInboxHandlerAsyncReplayTests
    {
        private const string OriginalCausationId = "original-causation-id";
        private const string OtherCausationId = "other-causation-id";

        private readonly FakeTimeProvider _timeProvider = new();
        private readonly MyCommand _command;
        private readonly InMemoryInbox _inbox;
        private readonly InMemoryOutbox _outbox;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly RequestContext _context;
        private readonly string _firstMatchingMessage;
        private readonly string _secondMatchingMessage;
        private readonly string _nonMatchingMessage;

        public UseInboxHandlerAsyncReplayTests()
        {
            MyStoredCommandToReplayHandlerAsync.ReceivedCount = 0;

            _inbox = new InMemoryInbox(_timeProvider);
            _outbox = new InMemoryOutbox(_timeProvider) { Tracer = new BrighterTracer() };
            _command = new MyCommand { Value = "My Test String" };
            var contextKey = typeof(MyStoredCommandToReplayHandlerAsync).FullName!;

            //Arrange — the command has already been seen, with a known causation id stored in the inbox
            var seedContext = new RequestContext();
            seedContext.Bag[RequestContextBagNames.CausationId] = OriginalCausationId;
            _inbox.Add(_command, contextKey, seedContext);

            //Arrange — the outbox holds dispatched messages for that causation, plus one for another causation
            _firstMatchingMessage = Guid.NewGuid().ToString();
            _secondMatchingMessage = Guid.NewGuid().ToString();
            _nonMatchingMessage = Guid.NewGuid().ToString();
            AddDispatchedMessage(_firstMatchingMessage, OriginalCausationId);
            AddDispatchedMessage(_secondMatchingMessage, OriginalCausationId);
            AddDispatchedMessage(_nonMatchingMessage, OtherCausationId);
            _timeProvider.Advance(TimeSpan.FromSeconds(10));

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandToReplayHandlerAsync>();

            var container = new ServiceCollection();
            container.AddTransient<MyStoredCommandToReplayHandlerAsync>();
            container.AddSingleton<IAmAnInboxAsync>(_inbox);
            container.AddSingleton<IAmACausationTrackingOutbox>(_outbox);
            container.AddTransient<UseInboxHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _context = new RequestContext();
            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Fact]
        public async Task When_handling_duplicate_command_async_with_replay_should_clear_outbox_dispatch()
        {
            //Act
            await _commandProcessor.SendAsync(_command, _context);

            //Assert — the handler is not re-executed
            Assert.Equal(0, MyStoredCommandToReplayHandlerAsync.ReceivedCount);

            //Assert — the matching causation's messages are outstanding again
            var outstanding = _outbox.OutstandingMessages(TimeSpan.Zero, _context).Select(m => m.Id.Value).ToArray();
            Assert.Contains(_firstMatchingMessage, outstanding);
            Assert.Contains(_secondMatchingMessage, outstanding);

            //Assert — the other causation's message is untouched and still dispatched
            var dispatched = _outbox.DispatchedMessages(TimeSpan.FromSeconds(5), _context).Select(m => m.Id.Value).ToArray();
            Assert.Contains(_nonMatchingMessage, dispatched);
            Assert.DoesNotContain(_nonMatchingMessage, outstanding);
        }

        private void AddDispatchedMessage(string id, string causationId)
        {
            var seedContext = new RequestContext();
            seedContext.Bag[RequestContextBagNames.CausationId] = causationId;
            var message = new Message(
                new MessageHeader(id, new RoutingKey("test_topic"), MessageType.MT_DOCUMENT),
                new MessageBody("message body"));
            _outbox.Add(message, seedContext);
            _outbox.MarkDispatched(id, seedContext, _timeProvider.GetUtcNow());
        }
    }
}
