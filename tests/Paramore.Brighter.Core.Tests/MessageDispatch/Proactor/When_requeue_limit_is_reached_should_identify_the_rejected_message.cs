#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

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
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.Defer.TestDoubles;
using Paramore.Brighter.Defer.Handlers;
using Paramore.Brighter.JsonConverters;
using Polly.Registry;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch.Proactor;

public class MessagePumpRequeueRejectionMessageIdAsyncTests
{
    [Theory]
    [InlineData(false, null, "current-message-id")]
    [InlineData(true, null, "current-message-id")]
    [InlineData(true, "", "current-message-id")]
    [InlineData(true, "original-message-id", "original-message-id")]
    public void When_requeue_limit_is_reached_should_identify_the_rejected_message(
        bool hasOriginalMessageId, string? originalMessageId, string expectedMessageId)
    {
        //Arrange
        var routingKey = new RoutingKey("requeue-message-id");
        var deadLetterRoutingKey = new RoutingKey("requeue-message-id-dlq");
        var bus = new InternalBus();
        var channel = new ChannelAsync(new ChannelName("requeue-message-id"), routingKey,
            new InMemoryMessageConsumer(routingKey, bus, new FakeTimeProvider(),
                deadLetterTopic: deadLetterRoutingKey));

        var subscriberRegistry = new SubscriberRegistry();
        subscriberRegistry.RegisterAsync<MyCommand, MyFailingDeferHandlerAsync>();
        var handlerFactory = new SimpleHandlerFactoryAsync(type =>
        {
            if (type == typeof(MyFailingDeferHandlerAsync))
                return new MyFailingDeferHandlerAsync();
            if (type == typeof(DeferMessageOnErrorHandlerAsync<MyCommand>))
                return new DeferMessageOnErrorHandlerAsync<MyCommand>();
            throw new ArgumentOutOfRangeException(nameof(type), type.Name, null);
        });
        var commandProcessor = new CommandProcessor(subscriberRegistry, handlerFactory,
            new InMemoryRequestContextFactory(), new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());

        var mapperRegistry = new MessageMapperRegistry(
            null,
            new SimpleMessageMapperFactoryAsync(_ => new MyCommandMessageMapperAsync()));
        mapperRegistry.RegisterAsync<MyCommand, MyCommandMessageMapperAsync>();
        var messagePump = new ServiceActivator.Proactor(commandProcessor, _ => typeof(MyCommand),
            mapperRegistry, new EmptyMessageTransformerFactoryAsync(),
            new InMemoryRequestContextFactory(), channel)
        {
            RequeueCount = 1
        };

        var message = new Message(
            new MessageHeader("current-message-id", routingKey, MessageType.MT_COMMAND),
            new MessageBody(JsonSerializer.Serialize(new MyCommand(), JsonSerialisationOptions.Options)));
        if (hasOriginalMessageId)
            message.Header.Bag[Message.OriginalMessageIdHeaderName] = originalMessageId!;
        channel.Enqueue(message);
        channel.Stop(routingKey);

        //Act
        messagePump.Run();

        //Assert
        var rejectedMessage = Assert.Single(bus.Stream(deadLetterRoutingKey));
        Assert.Equal(
            $"Message rejected reason: {RejectionReason.DeliveryError} Description: Handle Count Exceeded for message {expectedMessageId}",
            rejectedMessage.Header.Bag[Message.RejectionReasonHeaderName]);
    }
}
