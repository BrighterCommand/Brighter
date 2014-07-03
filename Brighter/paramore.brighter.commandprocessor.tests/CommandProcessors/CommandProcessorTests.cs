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
using Common.Logging.Simple;
using FakeItEasy;
using Machine.Specifications;
using Newtonsoft.Json;
using Polly;
using Polly.CircuitBreaker;
using TinyIoC;
using paramore.brighter.commandprocessor;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;
using Common.Logging;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject("Basic send of a command")]
    public class When_sending_a_command_to_the_processor
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDependentCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyDependentCommandHandler>(() => new MyDependentCommandHandler(new FakeRepository<MyAggregate>(new FakeSession()),logger));

            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(),  logger);
        };

        Because of = () => commandProcessor.Send(myCommand);

        It should_send_the_command_to_the_command_handler = () => MyCommandHandler.ShouldRecieve(myCommand).ShouldBeTrue();
    }

    [Subject("Commands should only have one handler")]
    public class When_there_are_multiple_possible_command_handlers
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        private static Exception exception;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDependentCommandHandler>();
            registry.Register<MyCommand, MyImplicitHandler>();
            
            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyCommandHandler>("DefaultHandler").AsMultiInstance();
            container.Register<IHandleRequests<MyCommand>, MyImplicitHandler>("ImplicitHandler").AsSingleton();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>().AsSingleton();
            container.Register<ILog, NoOpLogger>().AsSingleton();

            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(),  logger);
        };

        Because of = () =>  exception = Catch.Exception(() => commandProcessor.Send(myCommand));

        It should_fail_because_multiple_recievers_found = () => exception.ShouldBeAssignableTo(typeof (ArgumentException));
        It should_have_an_error_message_that_tells_you_why = () => exception.ShouldContainErrorMessage("More than one handler was found for the typeof command paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyCommand - a command should only have one handler.");
    }

    [Subject("Commands should have at least one handler")]
    public class When_there_are_no_command_handlers
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        private static Exception exception;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            commandProcessor = new CommandProcessor(new SubscriberRegistry(), new TinyIocHandlerFactory(new TinyIoCContainer()),  new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);

        };

        Because of = () =>  exception = Catch.Exception(() => commandProcessor.Send(myCommand));

        It should_fail_because_multiple_recievers_found = () => exception.ShouldBeAssignableTo(typeof (ArgumentException));
        It should_have_an_error_message_that_tells_you_why = () => exception.ShouldContainErrorMessage("No command handler was found for the typeof command paramore.commandprocessor.tests.CommandProcessors.TestDoubles.MyCommand - a command should have only one handler."); 
    }

    [Subject("Basic event publishing")]
    public class When_publishing_an_event_to_the_processor
    {
        static CommandProcessor commandProcessor;
        static readonly MyEvent myEvent = new MyEvent();

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            var handlerFactory = new TestHandlerFactory<MyEvent, MyEventHandler>(() => new MyEventHandler(logger));

            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(),  logger);
        };

        Because of = () => commandProcessor.Publish(myEvent);

        It should_publish_the_command_to_the_event_handlers = () => MyEventHandler.ShouldRecieve(myEvent).ShouldBeTrue();
    }

    [Subject("An event may have no subscribers")]
    public class When_there_are_no_subscribers
    {
        static CommandProcessor commandProcessor;
        static readonly MyEvent myEvent = new MyEvent();
        static Exception exception;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            var handlerFactory = new TestHandlerFactory<MyEvent, MyEventHandler>(() => new MyEventHandler(logger));

            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(),  logger);
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Publish(myEvent));

        It should_not_throw_an_exception = () => exception.ShouldBeNull();
    }

    [Subject("An event with multiple subscribers")]
    public class When_there_are_multiple_subscribers
    {
        static CommandProcessor commandProcessor;
        static readonly MyEvent myEvent = new MyEvent();
        static Exception exception;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyEvent, MyEventHandler>();
            registry.Register<MyEvent, MyEventHandler>();
            
            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyEvent>, MyEventHandler>().AsSingleton();
            container.Register<IHandleRequests<MyEvent>, MyEventHandler>().AsSingleton();

            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(),  logger);
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Publish(myEvent));

        It should_not_throw_an_exception = () => exception.ShouldBeNull();
        It should_publish_the_command_to_the_first_event_handler = () => MyEventHandler.ShouldRecieve(myEvent).ShouldBeTrue(); 
        It should_publish_the_command_to_the_second_event_handler = () => MyOtherEventHandler.ShouldRecieve(myEvent).ShouldBeTrue();
    }


    public class When_using_decoupled_invocation_to_send_a_message_asynchronously
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static Message message;
        static IAmAMessageStore<Message> commandRepository;
        static IAmAMessagingGateway messagingGateway ;
        
        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            myCommand.Value = "Hello World";
            commandRepository = A.Fake<IAmAMessageStore<Message>>();
            messagingGateway = A.Fake<IAmAMessagingGateway>();
            message = new Message(
                header: new MessageHeader(messageId: myCommand.Id, topic: "MyCommand", messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(myCommand))
                );

            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry(){{CommandProcessor.RETRYPOLICY, retryPolicy},{CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}},
                new MessageMapperRegistry(), 
                commandRepository, 
                messagingGateway, 
                logger);

        };

        Because of = () => commandProcessor.Post(myCommand);

        It should_store_the_message_in_the_sent_command_message_repository = () => commandRepository.Add(message);
        It should_send_a_message_via_the_messaging_gateway = () => A.CallTo(() => messagingGateway.Send(message)).MustHaveHappened();
    }

    public class When_resending_a_message_asynchronously
    {
        
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static Message message;
        static IAmAMessageStore<Message> commandRepository;
        static IAmAMessagingGateway messagingGateway ;
        
        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            myCommand.Value = "Hello World";
            commandRepository = A.Fake<IAmAMessageStore<Message>>();
            messagingGateway = A.Fake<IAmAMessagingGateway>();
            message = new Message(
                header: new MessageHeader(messageId: myCommand.Id, topic: "MyCommand",messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(myCommand))
                );
            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry();

            var circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreaker(1, TimeSpan.FromMilliseconds(1));

            A.CallTo(() => commandRepository.Get(message.Header.Id)).Returns(message);
            commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry(){{CommandProcessor.RETRYPOLICY, retryPolicy},{CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}},
                new MessageMapperRegistry(), 
                commandRepository, 
                messagingGateway, 
                logger);
        };

        Because of = () => commandProcessor.Repost(message.Header.Id);

        It should_send_a_message_via_the_messaging_gateway = () => A.CallTo(() => messagingGateway.Send(message)).MustHaveHappened();
    }

    public class When_an_exception_is_thrown_terminate_the_pipeline
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static Exception exception;

        private Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry() {{typeof (MyCommand), typeof (MyUnusedCommandHandler)}};
            var handlerFactory = new TestHandlerFactory<MyCommand, MyCommandHandler>(() => new MyCommandHandler(logger));
        
            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);
        };

        Because of = () => exception = Catch.Exception(() => commandProcessor.Send(myCommand));

        It should_throw_an_exception = () => exception.ShouldNotBeNull();
        It should_fail_the_pipeline_not_execute_it = () => MyUnusedCommandHandler.ShouldRecieve(myCommand).ShouldBeFalse();
    }

    public class When_there_are_no_failures_execute_all_the_steps_in_the_pipeline
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyPreAndPostDecoratedHandler>();
            registry.Register<MyCommand, MyLoggingHandler<MyCommand>>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyPreAndPostDecoratedHandler>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, MyValidationHandler<MyCommand>>().AsSingleton();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>().AsSingleton();
            container.Register<ILog, NoOpLogger>().AsSingleton();
            commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry(), logger);

        };

        Because of = () => commandProcessor.Send(myCommand);

        It should_call_the_pre_validation_handler = () => MyValidationHandler<MyCommand>.ShouldRecieve(myCommand).ShouldBeTrue();
        It should_send_the_command_to_the_command_handler = () => MyPreAndPostDecoratedHandler.ShouldRecieve(myCommand).ShouldBeTrue();
        It should_call_the_post_validation_handler = () => MyLoggingHandler<MyCommand>.ShouldRecieve(myCommand).ShouldBeTrue();
    }


    public class When_using_decoupled_invocation_messaging_gateway_throws_an_error_retry_n_times_then_break_circuit
    {
        static CommandProcessor commandProcessor;
        static readonly MyCommand myCommand = new MyCommand();
        static Message message;
        static IAmAMessageStore<Message> commandRepository;
        static IAmAMessagingGateway messagingGateway ;
        static Exception failedException;
        static IDisposable lifetime;
        static BrokenCircuitException circuitBrokenException;
        
        Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            myCommand.Value = "Hello World";
            commandRepository = A.Fake<IAmAMessageStore<Message>>();
            messagingGateway = A.Fake<IAmAMessagingGateway>();
            message = new Message(
                header: new MessageHeader(messageId: myCommand.Id, topic: "MyCommand",messageType: MessageType.MT_COMMAND),
                body: new MessageBody(JsonConvert.SerializeObject(myCommand))
                );

            A.CallTo(() => messagingGateway.Send(message)).Throws<Exception>().NumberOfTimes(4);

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


           commandProcessor = new CommandProcessor(
                new InMemoryRequestContextFactory(), 
                new PolicyRegistry(){{CommandProcessor.RETRYPOLICY, retryPolicy},{CommandProcessor.CIRCUITBREAKER, circuitBreakerPolicy}},
                new MessageMapperRegistry(), 
                commandRepository, 
                messagingGateway, 
                logger);       };

        Because of = () =>
            {
                //break circuit with retries
                failedException = Catch.Exception(() => commandProcessor.Post(myCommand));
                //now resond with broken ciruit
                circuitBrokenException = (BrokenCircuitException) Catch.Exception(() => commandProcessor.Post(myCommand));
            };

        It should_send_a_message_via_the_messaging_gateway = () => A.CallTo(() => messagingGateway.Send(message)).MustHaveHappened(Repeated.Exactly.Times(4));
        private It should_throw_a_exception_out_once_all_retries_exhausted = () => failedException.ShouldBeOfExactType(typeof(Exception));
        It should_throw_a_circuit_broken_exception_once_circuit_broken = () => circuitBrokenException.ShouldBeOfExactType(typeof(BrokenCircuitException));

        Cleanup tearDown = () => lifetime.Dispose();
    }
}

    
