#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class MessagePumpRequestRoutingTests
{
    public static TheoryData<MessagePumpType, MessageType, bool> RoutingCases
    {
        get
        {
            var cases = new TheoryData<MessagePumpType, MessageType, bool>();
            foreach (var pumpType in new[] { MessagePumpType.Reactor, MessagePumpType.Proactor })
            {
                foreach (var messageType in new[] { MessageType.MT_COMMAND, MessageType.MT_EVENT, MessageType.MT_DOCUMENT })
                {
                    cases.Add(pumpType, messageType, false);
                    cases.Add(pumpType, messageType, true);
                }
            }
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(RoutingCases))]
    public void When_mapped_event_has_multiple_handlers_should_publish_to_all_handlers(
        MessagePumpType pumpType, MessageType messageType, bool implementsInterface)
    {
        // Arrange
        var requestId = Id.Random();

        // Act
        var result = implementsInterface
            ? Run(new RoutingInterfaceEvent { Id = requestId }, pumpType, messageType, 2)
            : Run(new RoutingEvent { Id = requestId }, pumpType, messageType, 2);

        // Assert
        Assert.Equal(2, result.Handled.Length);
        Assert.All(result.Handled, request => Assert.Equal(requestId, request.Id));
        Assert.Empty(result.Rejected);
    }

    [Theory]
    [MemberData(nameof(RoutingCases))]
    public void When_mapped_command_has_multiple_handlers_should_not_publish_it(
        MessagePumpType pumpType, MessageType messageType, bool implementsInterface)
    {
        // Arrange
        var requestId = Id.Random();

        // Act
        var result = implementsInterface
            ? Run(new RoutingInterfaceCommand { Id = requestId }, pumpType, messageType, 2)
            : Run(new RoutingCommand { Id = requestId }, pumpType, messageType, 2);

        // Assert
        Assert.Empty(result.Handled);
        Assert.Empty(result.Rejected);
    }

    [Theory]
    [MemberData(nameof(RoutingCases))]
    public void When_mapped_command_has_one_handler_should_dispatch_to_it(
        MessagePumpType pumpType, MessageType messageType, bool implementsInterface)
    {
        // Arrange
        var requestId = Id.Random();

        // Act
        var result = implementsInterface
            ? Run(new RoutingInterfaceCommand { Id = requestId }, pumpType, messageType, 1)
            : Run(new RoutingCommand { Id = requestId }, pumpType, messageType, 1);

        // Assert
        Assert.Equal(requestId, Assert.Single(result.Handled).Id);
        Assert.Empty(result.Rejected);
    }

    [Theory]
    [InlineData(MessagePumpType.Reactor)]
    [InlineData(MessagePumpType.Proactor)]
    public void When_mapper_returns_bare_request_should_reject_as_unacceptable(MessagePumpType pumpType)
    {
        // Arrange
        var request = new RoutingBareRequest();

        // Act
        var result = Run(request, pumpType, MessageType.MT_EVENT, 1);

        // Assert
        Assert.Empty(result.Handled);
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(request.Id, rejected.Id);
        Assert.Contains("ICommand", rejected.Header.Bag[Message.RejectionReasonHeaderName].ToString());
        Assert.Contains("IEvent", rejected.Header.Bag[Message.RejectionReasonHeaderName].ToString());
    }

    private static (IRequest[] Handled, Message[] Rejected) Run<TRequest>(
        TRequest request, MessagePumpType pumpType, MessageType messageType, int handlerCount)
        where TRequest : class, IRequest
    {
        var handledRequests = new ConcurrentQueue<IRequest>();
        var subscriberRegistry = new SubscriberRegistry();
        IAmAHandlerFactory handlerFactory;
        if (pumpType == MessagePumpType.Reactor)
        {
            subscriberRegistry.Register<TRequest, RoutingRequestHandler<TRequest>>();
            if (handlerCount == 2)
                subscriberRegistry.Register<TRequest, AdditionalRoutingRequestHandler<TRequest>>();
            handlerFactory = new SimpleHandlerFactorySync(type => type == typeof(RoutingRequestHandler<TRequest>)
                ? new RoutingRequestHandler<TRequest>(handledRequests)
                : new AdditionalRoutingRequestHandler<TRequest>(handledRequests));
        }
        else
        {
            subscriberRegistry.RegisterAsync<TRequest, RoutingRequestHandlerAsync<TRequest>>();
            if (handlerCount == 2)
                subscriberRegistry.RegisterAsync<TRequest, AdditionalRoutingRequestHandlerAsync<TRequest>>();
            handlerFactory = new SimpleHandlerFactoryAsync(type => type == typeof(RoutingRequestHandlerAsync<TRequest>)
                ? new RoutingRequestHandlerAsync<TRequest>(handledRequests)
                : new AdditionalRoutingRequestHandlerAsync<TRequest>(handledRequests));
        }

        var commandProcessor = new CommandProcessor(subscriberRegistry, handlerFactory,
            new InMemoryRequestContextFactory(), new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var routingKey = new RoutingKey("request-routing");
        var invalidMessageKey = new RoutingKey("invalid-request-routing");
        var bus = new InternalBus();
        var consumer = new InMemoryMessageConsumer(routingKey, bus, new FakeTimeProvider(),
            invalidMessageTopic: invalidMessageKey);
        var mapperRegistry = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new JsonMessageMapper<TRequest>()),
            new SimpleMessageMapperFactoryAsync(_ => new JsonMessageMapper<TRequest>()));
        mapperRegistry.Register<TRequest, JsonMessageMapper<TRequest>>();
        mapperRegistry.RegisterAsync<TRequest, JsonMessageMapper<TRequest>>();
        var message = new Message(new MessageHeader(request.Id, routingKey, messageType),
            new MessageBody(JsonSerializer.Serialize(request, JsonSerialisationOptions.Options)));

        IAmAMessagePump pump;
        if (pumpType == MessagePumpType.Reactor)
        {
            var channel = new Channel(new ChannelName("request-routing"), routingKey, consumer);
            channel.Enqueue(message);
            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            pump = new ServiceActivator.Reactor(commandProcessor, _ => typeof(TRequest), mapperRegistry,
                null, new InMemoryRequestContextFactory(), channel);
        }
        else
        {
            var channel = new ChannelAsync(new ChannelName("request-routing"), routingKey, consumer);
            channel.Enqueue(message);
            channel.Enqueue(MessageFactory.CreateQuitMessage(routingKey));
            pump = new ServiceActivator.Proactor(commandProcessor, _ => typeof(TRequest), mapperRegistry,
                null, new InMemoryRequestContextFactory(), channel);
        }

        pump.Run();

        return (handledRequests.ToArray(), bus.Stream(invalidMessageKey).ToArray());
    }
}
