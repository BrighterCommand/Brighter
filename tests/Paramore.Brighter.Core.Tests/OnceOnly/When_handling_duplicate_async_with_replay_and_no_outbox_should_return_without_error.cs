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

using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.OnceOnly.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Inbox.Handlers;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.OnceOnly
{
    public class UseInboxHandlerAsyncReplayWithNoOutboxTests
    {
        private const string OriginalCausationId = "original-causation-id";

        private readonly MyCommand _command;
        private readonly IAmACommandProcessor _commandProcessor;

        public UseInboxHandlerAsyncReplayWithNoOutboxTests()
        {
            MyStoredCommandToReplayHandlerAsync.ReceivedCount = 0;

            var inbox = new InMemoryInbox(new FakeTimeProvider());
            _command = new MyCommand { Value = "My Test String" };
            var contextKey = typeof(MyStoredCommandToReplayHandlerAsync).FullName!;

            //Arrange — the command has already been seen, with a causation id stored in the inbox
            var seedContext = new RequestContext();
            seedContext.Bag[RequestContextBagNames.CausationId] = OriginalCausationId;
            inbox.Add(_command, contextKey, seedContext);

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyStoredCommandToReplayHandlerAsync>();

            //Arrange — NO outbox is registered, so UseInboxHandlerAsync receives null for its optional outbox
            var container = new ServiceCollection();
            container.AddTransient<MyStoredCommandToReplayHandlerAsync>();
            container.AddSingleton<IAmAnInboxAsync>(inbox);
            container.AddTransient<UseInboxHandlerAsync<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Fact]
        public async Task When_handling_duplicate_async_with_replay_and_no_outbox_should_return_without_error()
        {
            //Act — a duplicate command with Replay configured but no outbox available
            var exception = await Record.ExceptionAsync(() => _commandProcessor.SendAsync(_command));

            //Assert — the handler returns without throwing and is not re-executed
            Assert.Null(exception);
            Assert.Equal(0, MyStoredCommandToReplayHandlerAsync.ReceivedCount);
        }
    }
}
