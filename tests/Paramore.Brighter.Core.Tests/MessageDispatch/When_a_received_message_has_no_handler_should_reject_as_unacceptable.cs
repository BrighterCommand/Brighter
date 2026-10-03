#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class ReceivedMessageWithoutHandlerTests
{
    private readonly RoutingKey _topic = new("received-without-handler");
    private readonly RoutingKey _invalidTopic = new("received-without-handler-invalid");
    private readonly RoutingKey _deadLetterTopic = new("received-without-handler-dead-letter");
    private readonly InternalBus _bus = new();

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void When_a_received_message_has_no_handler_should_reject_as_unacceptable(
        bool useProactor, bool isEvent, bool useDeadLetterFallback)
    {
        if (isEvent)
            VerifyMissingHandler<UnhandledReceivedEvent>(useProactor, useDeadLetterFallback);
        else
            VerifyMissingHandler<UnhandledReceivedCommand>(useProactor, useDeadLetterFallback);
    }

    private void VerifyMissingHandler<TRequest>(bool useProactor, bool useDeadLetterFallback)
        where TRequest : class, IRequest, new()
    {
        //Arrange
        var processor = CreateProcessor<TRequest>(useProactor, new SubscriberRegistry());
        var message = Map(new TRequest());
        var originalId = message.Id;
        var originalBody = message.Body.Value;
        _bus.Enqueue(message);
        _bus.Enqueue(MessageFactory.CreateQuitMessage(_topic));
        var pump = CreatePump<TRequest>(useProactor, processor, useDeadLetterFallback);

        //Act
        pump.Run();

        //Assert
        Assert.Equal(MessagePumpStatus.MP_STOPPED, pump.Status);
        var rejected = Assert.Single(_bus.Stream(useDeadLetterFallback ? _deadLetterTopic : _invalidTopic));
        Assert.Equal(originalId, rejected.Id);
        Assert.Equal(originalBody, rejected.Body.Value);
        var reason = Assert.IsType<string>(rejected.Header.Bag[Message.RejectionReasonHeaderName]);
        Assert.Contains("handler", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(typeof(TRequest).FullName!, reason);
        Assert.Empty(_bus.Stream(_topic));
        Assert.Empty(_bus.Stream(useDeadLetterFallback ? _invalidTopic : _deadLetterTopic));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void When_runtime_routing_selects_no_handler_should_reject_and_continue(
        bool useProactor, bool isEvent)
    {
        if (isEvent)
            VerifyEmptySelection<UnhandledReceivedEvent>(useProactor);
        else
            VerifyEmptySelection<UnhandledReceivedCommand>(useProactor);
    }

    private void VerifyEmptySelection<TRequest>(bool useProactor) where TRequest : class, IRequest, new()
    {
        //Arrange
        var before = new TRequest();
        var unhandled = new TRequest();
        var after = new TRequest();
        var handled = new List<Id>();
        var selections = new List<Id>();
        var subscribers = new SubscriberRegistry();
        var handlerType = useProactor
            ? typeof(ReceivedRequestHandlerAsync<TRequest>)
            : typeof(ReceivedRequestHandler<TRequest>);
        List<Type> Select(IRequest? request, IRequestContext? context)
        {
            selections.Add(request!.Id);
            return request.Id == unhandled.Id ? [] : [handlerType];
        }

        if (useProactor)
            subscribers.RegisterAsync<TRequest>(Select, [handlerType]);
        else
            subscribers.Register<TRequest>(Select, [handlerType]);

        var processor = CreateProcessor<TRequest>(useProactor, subscribers,
            (request, _) => handled.Add(request.Id),
            (request, _) =>
            {
                handled.Add(request.Id);
                return Task.CompletedTask;
            });
        var unhandledMessage = Map(unhandled);
        var originalBody = unhandledMessage.Body.Value;
        _bus.Enqueue(Map(before));
        _bus.Enqueue(unhandledMessage);
        _bus.Enqueue(Map(after));
        _bus.Enqueue(MessageFactory.CreateQuitMessage(_topic));
        var pump = CreatePump<TRequest>(useProactor, processor, dynamicRouting: true);

        //Act
        pump.Run();

        //Assert
        Assert.Equal(MessagePumpStatus.MP_STOPPED, pump.Status);
        Assert.Equal(new[] { before.Id, after.Id }, handled);
        Assert.Equal(new[] { before.Id, unhandled.Id, after.Id }, selections);
        var rejected = Assert.Single(_bus.Stream(_invalidTopic));
        Assert.Equal(unhandled.Id, rejected.Id);
        Assert.Equal(originalBody, rejected.Body.Value);
        Assert.Contains("handler", Assert.IsType<string>(
            rejected.Header.Bag[Message.RejectionReasonHeaderName]), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_bus.Stream(_deadLetterTopic));
        Assert.Empty(_bus.Stream(_topic));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_publishing_locally_without_handlers_should_complete(bool useProactor)
    {
        //Arrange
        var processor = CreateProcessor<UnhandledNestedEvent>(useProactor, new SubscriberRegistry());

        //Act
        var exception = useProactor
            ? await Record.ExceptionAsync(() => processor.PublishAsync(new UnhandledNestedEvent()))
            : Record.Exception(() => processor.Publish(new UnhandledNestedEvent()));

        //Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void When_a_received_handler_publishes_an_unhandled_local_event_should_complete(
        bool useProactor, bool isEvent)
    {
        if (isEvent)
            VerifyNestedPublication<UnhandledReceivedEvent>(useProactor);
        else
            VerifyNestedPublication<UnhandledReceivedCommand>(useProactor);
    }

    private void VerifyNestedPublication<TRequest>(bool useProactor) where TRequest : class, IRequest, new()
    {
        //Arrange
        var subscribers = new SubscriberRegistry();
        if (useProactor)
            subscribers.RegisterAsync<TRequest, ReceivedRequestHandlerAsync<TRequest>>();
        else
            subscribers.Register<TRequest, ReceivedRequestHandler<TRequest>>();

        CommandProcessor processor = null!;
        var completed = false;
        processor = CreateProcessor<TRequest>(useProactor, subscribers,
            (_, context) =>
            {
                Assert.NotNull(context.OriginatingMessage);
                processor.Publish(new UnhandledNestedEvent(), (RequestContext)context);
                completed = true;
            },
            async (_, context) =>
            {
                Assert.NotNull(context.OriginatingMessage);
                await processor.PublishAsync(new UnhandledNestedEvent(), (RequestContext)context);
                completed = true;
            });
        _bus.Enqueue(Map(new TRequest()));
        _bus.Enqueue(MessageFactory.CreateQuitMessage(_topic));
        var pump = CreatePump<TRequest>(useProactor, processor);

        //Act
        pump.Run();

        //Assert
        Assert.True(completed);
        Assert.Equal(MessagePumpStatus.MP_STOPPED, pump.Status);
        Assert.Empty(_bus.Stream(_invalidTopic));
        Assert.Empty(_bus.Stream(_deadLetterTopic));
        Assert.Empty(_bus.Stream(_topic));
    }

    private static CommandProcessor CreateProcessor<TRequest>(
        bool useProactor,
        SubscriberRegistry subscribers,
        Action<TRequest, IRequestContext>? handle = null,
        Func<TRequest, IRequestContext, Task>? handleAsync = null)
        where TRequest : class, IRequest
    {
        IAmAHandlerFactory factory = useProactor
            ? new SimpleHandlerFactoryAsync(_ => new ReceivedRequestHandlerAsync<TRequest>(
                handleAsync ?? ((_, _) => throw new InvalidOperationException("Unexpected handler invocation"))))
            : new SimpleHandlerFactorySync(_ => new ReceivedRequestHandler<TRequest>(
                handle ?? ((_, _) => throw new InvalidOperationException("Unexpected handler invocation"))));
        return new CommandProcessor(subscribers, factory, new InMemoryRequestContextFactory(),
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
    }

    private Message Map<TRequest>(TRequest request) where TRequest : class, IRequest
        => new JsonMessageMapper<TRequest>().MapToMessage(request,
            new Publication { Topic = _topic, Type = new CloudEventsType(typeof(TRequest).FullName!) });

    private IAmAMessagePump CreatePump<TRequest>(
        bool useProactor,
        CommandProcessor processor,
        bool useDeadLetterFallback = false,
        bool dynamicRouting = false)
        where TRequest : class, IRequest
    {
        var mappers = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new JsonMessageMapper<TRequest>()),
            new SimpleMessageMapperFactoryAsync(_ => new JsonMessageMapper<TRequest>()));
        mappers.Register<TRequest, JsonMessageMapper<TRequest>>();
        mappers.RegisterAsync<TRequest, JsonMessageMapper<TRequest>>();
        var consumer = new InMemoryMessageConsumer(_topic, _bus, new FakeTimeProvider(),
            deadLetterTopic: _deadLetterTopic, invalidMessageTopic: useDeadLetterFallback ? null : _invalidTopic);
        var contextFactory = new InMemoryRequestContextFactory();
        var requestTypes = new Dictionary<string, Type> { [typeof(TRequest).FullName!] = typeof(TRequest) };
        Type GetRequestType(Message message) => dynamicRouting
            ? requestTypes[message.Header.Type.Value]
            : typeof(TRequest);

        return useProactor
            ? new ServiceActivator.Proactor(processor, GetRequestType, mappers,
                new EmptyMessageTransformerFactoryAsync(), contextFactory,
                new ChannelAsync(new ChannelName("unhandled-received-message"), _topic, consumer))
                { UnacceptableMessageLimit = 0 }
            : new ServiceActivator.Reactor(processor, GetRequestType, mappers,
                new EmptyMessageTransformerFactory(), contextFactory,
                new Channel(new ChannelName("unhandled-received-message"), _topic, consumer))
                { UnacceptableMessageLimit = 0 };
    }
}
