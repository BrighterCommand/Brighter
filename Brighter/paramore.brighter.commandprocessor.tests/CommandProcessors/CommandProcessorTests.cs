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
using System.Threading.Tasks;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;
using Polly;
using Polly.CircuitBreaker;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject("Basic send of a command")]
    public class When_sending_a_command_to_the_processor
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyCommandHandler>(() => new MyCommandHandler(logger));

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_send_the_command_to_the_command_handler = () => MyCommandHandler.Shouldreceive(s_myCommand).ShouldBeTrue();
    }

    [Subject("Commands should only have one handler")]
    public class When_there_are_multiple_possible_command_handlers
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            registry.Register<MyCommand, MyImplicitHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyCommandHandler>("DefaultHandler");
            container.Register<IHandleRequests<MyCommand>, MyImplicitHandler>("ImplicitHandler");
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));

        private It _should_fail_because_multiple_receivers_found = () => s_exception.ShouldBeAssignableTo(typeof(ArgumentException));
        private It _should_have_an_error_message_that_tells_you_why = () => s_exception.ShouldContainErrorMessage("More than one handler was found for the typeof command paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyCommand - a command should only have one handler.");
    }

    [Subject("Commands should have at least one handler")]
    public class When_there_are_no_command_handlers
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_commandProcessor = new CommandProcessor(new SubscriberRegistry(), new TinyIocHandlerFactory(new TinyIoCContainer()), new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));

        private It _should_fail_because_multiple_receivers_found = () => s_exception.ShouldBeAssignableTo(typeof(ArgumentException));
        private It _should_have_an_error_message_that_tells_you_why = () => s_exception.ShouldContainErrorMessage("No command handler was found for the typeof command paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyCommand - a command should have exactly one handler.");
    }

    [Subject("Basic event publishing")]
    public class When_publishing_an_event_to_the_processor
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyEvent s_myEvent = new MyEvent();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            var handlerFactory = new TestHandlerFactory<MyEvent, MyEventHandler>(() => new MyEventHandler(logger));

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_commandProcessor.Publish(s_myEvent);
        private It _should_publish_the_command_to_the_event_handlers = () => MyEventHandler.Shouldreceive(s_myEvent).ShouldBeTrue();
    }

    [Subject("An event may have no subscribers")]
    public class When_there_are_no_subscribers
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyEvent s_myEvent = new MyEvent();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            var handlerFactory = new TestHandlerFactory<MyEvent, MyEventHandler>(() => new MyEventHandler(logger));

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Publish(s_myEvent));

        private It _should_not_throw_an_exception = () => s_exception.ShouldBeNull();
    }

    [Subject("An event with multiple subscribers")]
    public class When_there_are_multiple_subscribers
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyEvent s_myEvent = new MyEvent();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            registry.Register<MyEvent, MyOtherEventHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyEvent>, MyEventHandler>("MyEventHandler");
            container.Register<IHandleRequests<MyEvent>, MyOtherEventHandler>("MyOtherHandler");
            container.Register<ILog>(logger);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Publish(s_myEvent));

        private It _should_not_throw_an_exception = () => s_exception.ShouldBeNull();
        private It _should_publish_the_command_to_the_first_event_handler = () => MyEventHandler.Shouldreceive(s_myEvent).ShouldBeTrue();
        private It _should_publish_the_command_to_the_second_event_handler = () => MyOtherEventHandler.Shouldreceive(s_myEvent).ShouldBeTrue();
    }

    [Subject("An event with multiple subscribers")]
    public class When_publishing_to_multiple_subscribers_should_aggregate_exceptions
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyEvent s_myEvent = new MyEvent();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            registry.Register<MyEvent, MyOtherEventHandler>();
            registry.Register<MyEvent, MyThrowingEventHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyEvent>, MyEventHandler>("MyEventHandler");
            container.Register<IHandleRequests<MyEvent>, MyOtherEventHandler>("MyOtherHandler");
            container.Register<IHandleRequests<MyEvent>, MyThrowingEventHandler>("MyThrowingHandler");
            container.Register<ILog>(logger);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Publish(s_myEvent));

        private It _should_throw_an_aggregate_exception = () => s_exception.ShouldBeOfExactType(typeof(AggregateException));
        private It _should_have_an_inner_exception_from_the_handler = () => ((AggregateException)s_exception).InnerException.ShouldBeOfExactType(typeof(InvalidOperationException));
        private It _should_publish_the_command_to_the_first_event_handler = () => MyEventHandler.Shouldreceive(s_myEvent).ShouldBeTrue();
        private It _should_publish_the_command_to_the_second_event_handler = () => MyOtherEventHandler.Shouldreceive(s_myEvent).ShouldBeTrue();
    }


    public class When_using_decoupled_invocation_to_send_a_message_asynchronously
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Message s_message;
        private static FakeMessageStore s_fakeMessageStore;
        private static FakeMessageProducer s_fakeMessageProducer;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_myCommand.Value = "Hello World";

            s_fakeMessageStore = new FakeMessageStore();
            s_fakeMessageProducer = new FakeMessageProducer();

            s_message = new Message(
                header: new MessageHeader(messageId: s_myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(s_myCommand))
                );

            var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            s_commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                messageMapperRegistry,
                s_fakeMessageStore,
                s_fakeMessageProducer,
                logger);
        };

        private Because _of = () => s_commandProcessor.Post(s_myCommand);

        private Cleanup cleanup = () => s_commandProcessor.Dispose();

        private It _should_store_the_message_in_the_sent_command_message_repository = () => s_fakeMessageStore.MessageWasAdded.ShouldBeTrue();
        private It _should_send_a_message_via_the_messaging_gateway = () => s_fakeMessageProducer.MessageWasSent.ShouldBeTrue();
    }

    public class When_resending_a_message_asynchronously
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Message s_message;
        private static IAmAMessageStore<Message> s_messageStore;
        private static IAmAMessageProducer s_messagingGateway;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_myCommand.Value = "Hello World";
            s_messageStore = A.Fake<IAmAMessageStore<Message>>();
            var tcs = new TaskCompletionSource<object>();
            tcs.SetResult(new object());

            s_messagingGateway = A.Fake<IAmAMessageProducer>();
            s_message = new Message(
                header: new MessageHeader(messageId: s_myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(s_myCommand))
                );
            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            s_commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(),
                new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                new MessageMapperRegistry(new TinyIoCMessageMapperFactory(new TinyIoCContainer())),
                s_messageStore,
                s_messagingGateway,
                logger);

            A.CallTo(() => s_messageStore.Get(A<Guid>.Ignored, A<int>.Ignored)).Returns(s_message);
        };

        private Because _of = () => s_commandProcessor.Repost(s_message.Header.Id);

        private Cleanup cleanup = () => s_commandProcessor.Dispose();

        private It _should_send_a_message_via_the_messaging_gateway = () => A.CallTo(() => s_messagingGateway.Send(s_message)).MustHaveHappened();
    }

    public class When_an_exception_is_thrown_terminate_the_pipeline
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Exception s_exception;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyUnusedCommandHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyUnusedCommandHandler>();
            container.Register<IHandleRequests<MyCommand>, MyAbortingHandler<MyCommand>>();
            container.Register<ILog>(logger);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_exception = Catch.Exception(() => s_commandProcessor.Send(s_myCommand));

        private Cleanup cleanup = () => s_commandProcessor.Dispose();

        private It _should_throw_an_exception = () => s_exception.ShouldNotBeNull();
        private It _should_fail_the_pipeline_not_execute_it = () => MyUnusedCommandHandler.Shouldreceive(s_myCommand).ShouldBeFalse();
    }

    public class When_there_are_no_failures_execute_all_the_steps_in_the_pipeline
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>();
            container.Register<IHandleRequests<MyCommand>, MyValidationHandler<MyCommand>>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();
            container.Register<ILog>(logger);
            s_commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_call_the_pre_validation_handler = () => MyValidationHandler<MyCommand>.Shouldreceive(s_myCommand).ShouldBeTrue();
        private It _should_send_the_command_to_the_command_handler = () => MyPreAndPostDecoratedHandler.Shouldreceive(s_myCommand).ShouldBeTrue();
        private It _should_call_the_post_validation_handler = () => MyLoggingHandler<MyCommand>.Shouldreceive(s_myCommand).ShouldBeTrue();
    }


    public class When_using_decoupled_invocation_messaging_gateway_throws_an_error_retry_n_times_then_break_circuit
    {
        private static CommandProcessor s_commandProcessor;
        private static readonly MyCommand s_myCommand = new MyCommand();
        private static Message s_message;
        private static FakeMessageStore s_messageStore;
        private static FakeErroringMessageProducer s_messagingProducer;
        private static Exception s_failedException;
        private static BrokenCircuitException s_circuitBrokenException;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            s_myCommand.Value = "Hello World";
            s_messageStore = new FakeMessageStore();

            s_messagingProducer = new FakeErroringMessageProducer();
            s_message = new Message(
                header: new MessageHeader(messageId: s_myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(s_myCommand))
                );
            var messageMapperRegistry = new MessageMapperRegistry(new TestMessageMapperFactory(() => new MyCommandMessageMapper()));
            messageMapperRegistry.Register<MyCommand, MyCommandMessageMapper>();

            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                    {
                        TimeSpan.FromMilliseconds(50),
                        TimeSpan.FromMilliseconds(100),
                        TimeSpan.FromMilliseconds(150)
                    });

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMinutes(1));


            s_commandProcessor = new CommandProcessor(
                 new InMemoryRequestContextFactory(),
                 new PolicyRegistry() { { CommandProcessor.RETRYPOLICY, retryPolicy }, { CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy } },
                 messageMapperRegistry,
                 s_messageStore,
                 s_messagingProducer,
                 logger);
        };

        private Because _of = () =>
        {
            //break circuit with retries
            s_failedException = Catch.Exception(() => s_commandProcessor.Post(s_myCommand));
            //now resond with broken ciruit
            s_circuitBrokenException = (BrokenCircuitException)Catch.Exception(() => s_commandProcessor.Post(s_myCommand));
        };

        private Cleanup cleanup = () => s_commandProcessor.Dispose();

        private It _should_send_messages_via_the_messaging_gateway = () => s_messagingProducer.SentCalledCount.ShouldEqual(4);
        private It _should_throw_a_exception_out_once_all_retries_exhausted = () => s_failedException.ShouldBeOfExactType(typeof(Exception));
        private It _should_throw_a_circuit_broken_exception_once_circuit_broken = () => s_circuitBrokenException.ShouldBeOfExactType(typeof(BrokenCircuitException));
    }
}


