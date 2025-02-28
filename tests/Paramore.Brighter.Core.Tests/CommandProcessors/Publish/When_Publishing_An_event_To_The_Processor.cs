#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using FluentAssertions;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CommandProcessors.Publish
{
    [Collection("CommandProcessor")]
    public class CommandProcessorPublishEventTests : IDisposable
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, string> _receivedMessages = new Dictionary<string, string>();
        private readonly MyEvent _myEvent = new();

        public CommandProcessorPublishEventTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            var handlerFactory = new SimpleHandlerFactorySync(_ => new MyEventHandler(_receivedMessages));

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), new InMemorySchedulerFactory());
            PipelineBuilder<MyEvent>.ClearPipelineCache();
        }

        [Fact]
        public void When_Publishing_An_Event_To_The_Processor()
        {
            _commandProcessor.Publish(_myEvent);

           //_should_publish_the_command_to_the_event_handlers
            _receivedMessages.Should().Contain(nameof(MyEventHandler), _myEvent.Id);
        }

        public void Dispose()
        {
            CommandProcessor.ClearServiceBus();
        }
    }
}
