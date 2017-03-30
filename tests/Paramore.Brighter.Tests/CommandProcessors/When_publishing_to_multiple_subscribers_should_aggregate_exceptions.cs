#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class PublishingToMultipleSubscribersTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly IDictionary<string, Guid> _receivedMessages = new Dictionary<string, Guid>();
        private readonly MyEvent _myEvent = new MyEvent();
        private Exception _exception;

        public PublishingToMultipleSubscribersTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            registry.Register<MyEvent, MyOtherEventHandler>();
            registry.Register<MyEvent, MyThrowingEventHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyEvent>, MyEventHandler>();
            container.Register<IHandleRequests<MyEvent>, MyOtherEventHandler>();
            container.Register<IHandleRequests<MyEvent>, MyThrowingEventHandler>();
            container.Register(_receivedMessages);

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Fact]
        public void When_Publishing_To_Multiple_Subscribers_Should_Aggregate_Exceptions()
        {
            _exception = Catch.Exception(() => _commandProcessor.Publish(_myEvent));

            //_should_throw_an_aggregate_exception
            _exception.Should().BeOfType<AggregateException>();
            //_should_have_an_inner_exception_from_the_handler
            ((AggregateException)_exception).InnerException.Should().BeOfType<InvalidOperationException>();
            //_should_publish_the_command_to_the_first_event_handler
            _receivedMessages.Should().Contain(nameof(MyEventHandler), _myEvent.Id);
            //_should_publish_the_command_to_the_second_event_handler
            _receivedMessages.Should().Contain(nameof(MyOtherEventHandler), _myEvent.Id);
        }
    }
}