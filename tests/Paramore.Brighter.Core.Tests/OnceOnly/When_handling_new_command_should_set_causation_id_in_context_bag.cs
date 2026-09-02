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
    public class UseInboxHandlerCausationTrackingTests
    {
        private readonly MyCommand _command;
        private readonly InMemoryInbox _inbox;
        private readonly IAmACommandProcessor _commandProcessor;
        private readonly string _contextKey;

        public UseInboxHandlerCausationTrackingTests()
        {
            _inbox = new InMemoryInbox(new FakeTimeProvider());

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStoredCommandHandler>();

            var container = new ServiceCollection();
            container.AddTransient<MyStoredCommandHandler>();
            container.AddSingleton<IAmAnInboxSync>(_inbox);
            container.AddTransient<UseInboxHandler<MyCommand>>();
            container.AddSingleton<IBrighterOptions>(new BrighterOptions { HandlerLifetime = ServiceLifetime.Transient });

            var handlerFactory = new ServiceProviderHandlerFactory(container.BuildServiceProvider());

            _command = new MyCommand { Value = "My Test String" };
            _contextKey = typeof(MyStoredCommandHandler).FullName!;

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(),
                new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        }

        [Fact]
        public void When_handling_new_command_should_set_causation_id_in_context_bag()
        {
            //Arrange
            var requestContext = new RequestContext();

            //Act
            _commandProcessor.Send(_command, requestContext);

            //Assert — the causation id defaults to the command's id and is shared via the context bag
            Assert.True(requestContext.Bag.ContainsKey(RequestContextBagNames.CausationId));
            Assert.Equal(_command.Id.Value, requestContext.Bag[RequestContextBagNames.CausationId]);

            //Assert — the inbox entry carries the same causation id
            var storedCausationId = ((IAmACausationTrackingInbox)_inbox)
                .GetCausationId(_command.Id, _contextKey, requestContext);
            Assert.Equal(_command.Id.Value, storedCausationId);
        }

        [Fact]
        public void When_handling_new_command_with_a_causation_id_already_in_context_should_preserve_it()
        {
            //Arrange — a parent handler earlier in the pipeline has already stamped a causation id into the
            //shared context bag (this is the causation-chaining/linking semantic the feature is named for)
            const string parentCausationId = "parent-causation-id";
            var requestContext = new RequestContext();
            requestContext.Bag[RequestContextBagNames.CausationId] = parentCausationId;

            //Act
            _commandProcessor.Send(_command, requestContext);

            //Assert — the handler must NOT overwrite the inherited causation id with the command's own id
            Assert.Equal(parentCausationId, requestContext.Bag[RequestContextBagNames.CausationId]);

            //Assert — the inbox entry is linked to the parent causation, not the command id
            var storedCausationId = ((IAmACausationTrackingInbox)_inbox)
                .GetCausationId(_command.Id, _contextKey, requestContext);
            Assert.Equal(parentCausationId, storedCausationId);
        }
    }
}
