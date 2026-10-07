#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia <irakli.gabisonia94@gmail.com>

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

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.JsonConverters;
using Paramore.Brighter.MessageMappers;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class MessageTypeMismatchContinuationTests
{
    [Theory]
    [InlineData(MessagePumpType.Reactor)]
    [InlineData(MessagePumpType.Proactor)]
    public void When_a_command_has_an_event_header_should_acknowledge_it_and_process_the_next_command(
        MessagePumpType pumpType)
    {
        //Arrange
        var mismatchedCommand = new RoutingCommand();
        var subsequentCommand = new RoutingCommand();
        var topic = new RoutingKey("message-type-mismatch-continuation");
        var mismatchedMessage = new Message(
            new MessageHeader(mismatchedCommand.Id, topic, MessageType.MT_EVENT),
            new MessageBody(JsonSerializer.Serialize(mismatchedCommand, JsonSerialisationOptions.Options)));
        var subsequentMessage = new Message(
            new MessageHeader(subsequentCommand.Id, topic, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(subsequentCommand, JsonSerialisationOptions.Options)));
        using var consumer = new InMemoryFailingRequeueConsumer(
            [mismatchedMessage, subsequentMessage, MessageFactory.CreateQuitMessage(topic)], failNack: false);
        var handledRequests = new ConcurrentQueue<IRequest>();
        var subscribers = new SubscriberRegistry();
        var mappers = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => new JsonMessageMapper<RoutingCommand>()),
            new SimpleMessageMapperFactoryAsync(_ => new JsonMessageMapper<RoutingCommand>()));
        IAmAHandlerFactory handlers;

        if (pumpType == MessagePumpType.Reactor)
        {
            subscribers.Register<RoutingCommand, RoutingRequestHandler<RoutingCommand>>();
            handlers = new SimpleHandlerFactorySync(_ => new RoutingRequestHandler<RoutingCommand>(handledRequests));
            mappers.Register<RoutingCommand, JsonMessageMapper<RoutingCommand>>();
        }
        else
        {
            subscribers.RegisterAsync<RoutingCommand, RoutingRequestHandlerAsync<RoutingCommand>>();
            handlers = new SimpleHandlerFactoryAsync(_ => new RoutingRequestHandlerAsync<RoutingCommand>(handledRequests));
            mappers.RegisterAsync<RoutingCommand, JsonMessageMapper<RoutingCommand>>();
        }

        var contextFactory = new InMemoryRequestContextFactory();
        var processor = new Brighter.CommandProcessor(subscribers, handlers, contextFactory,
            new PolicyRegistry(), new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var channelName = new ChannelName("message-type-mismatch-continuation");
        IAmAMessagePump pump = pumpType == MessagePumpType.Reactor
            ? new ServiceActivator.Reactor(processor, _ => typeof(RoutingCommand), mappers,
                new EmptyMessageTransformerFactory(), contextFactory, new Channel(channelName, topic, consumer))
            : new ServiceActivator.Proactor(processor, _ => typeof(RoutingCommand), mappers,
                new EmptyMessageTransformerFactoryAsync(), contextFactory, new ChannelAsync(channelName, topic, consumer));

        //Act
        Exception? exception = Record.Exception(pump.Run);

        //Assert
        Assert.Null(exception);
        Id[] expectedIds = [mismatchedCommand.Id, subsequentCommand.Id];
        Id[] handledIds = [.. handledRequests.Select(request => request.Id)];
        Assert.Equal(expectedIds, handledIds);
        Assert.Equal(expectedIds, consumer.Acknowledged);
        Assert.Empty(consumer.Rejected);
        Assert.Empty(consumer.RequeueAttempts);
        Assert.Empty(consumer.NegativelyAcknowledged);
        Assert.Equal(MessagePumpStatus.MP_STOPPED, pump.Status);
    }
}
