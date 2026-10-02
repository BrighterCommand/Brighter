#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia <irakli.gabisonia94@gmail.com>

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
using Paramore.Brighter.Core.Tests.CommandProcessors.TestDoubles;
using Paramore.Brighter.Core.Tests.MessageDispatch.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Polly.Registry;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.MessageDispatch;

public class MessagePumpRequeueFailureTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void When_requeue_fails_should_log_leave_message_unacknowledged_and_continue(bool useProactor, bool isEvent, bool failNack)
    {
        if (isEvent)
            VerifyRequeueFailure<RequeueFailureEvent>(useProactor, failNack);
        else
            VerifyRequeueFailure<RequeueFailureCommand>(useProactor, failNack);
    }

    private static void VerifyRequeueFailure<TRequest>(bool useProactor, bool failNack) where TRequest : class, IRequest, new()
    {
        //Arrange
        using var logs = TestCorrelator.CreateContext();
        var deferred = new TRequest();
        var subsequent = new TRequest();
        var topic = new RoutingKey("requeue-failure");
        var publication = new Publication { Topic = topic };
        var mapper = new RequeueFailureMessageMapper<TRequest>();
        var failedMessage = mapper.MapToMessage(deferred, publication);
        var subsequentMessage = mapper.MapToMessage(subsequent, publication);
        using var consumer = new InMemoryFailingRequeueConsumer(
            [failedMessage, subsequentMessage, MessageFactory.CreateQuitMessage(topic)], failNack);
        var handled = new List<Id>();
        var subscribers = new SubscriberRegistry();
        var mappers = new MessageMapperRegistry(
            new SimpleMessageMapperFactory(_ => mapper),
            new SimpleMessageMapperFactoryAsync(_ => mapper));
        IAmAHandlerFactory handlers;

        if (useProactor)
        {
            subscribers.RegisterAsync<TRequest, RequeueFailureHandlerAsync<TRequest>>();
            handlers = new SimpleHandlerFactoryAsync(_ => new RequeueFailureHandlerAsync<TRequest>(deferred.Id, handled));
            mappers.RegisterAsync<TRequest, RequeueFailureMessageMapper<TRequest>>();
        }
        else
        {
            subscribers.Register<TRequest, RequeueFailureHandler<TRequest>>();
            handlers = new SimpleHandlerFactorySync(_ => new RequeueFailureHandler<TRequest>(deferred.Id, handled));
            mappers.Register<TRequest, RequeueFailureMessageMapper<TRequest>>();
        }

        PipelineBuilder<TRequest>.ClearPipelineCache();
        var contextFactory = new InMemoryRequestContextFactory();
        var processor = new Brighter.CommandProcessor(
            subscribers, handlers, contextFactory, new PolicyRegistry(),
            new ResiliencePipelineRegistry<string>(), new InMemorySchedulerFactory());
        var channelName = new ChannelName("requeue-failure");
        IAmAMessagePump pump = useProactor
            ? new ServiceActivator.Proactor(processor, _ => typeof(TRequest), mappers,
                new EmptyMessageTransformerFactoryAsync(), contextFactory,
                new ChannelAsync(channelName, topic, consumer)) { RequeueCount = -1, DontAckDelay = TimeSpan.Zero }
            : new ServiceActivator.Reactor(processor, _ => typeof(TRequest), mappers,
                new EmptyMessageTransformerFactory(), contextFactory,
                new Channel(channelName, topic, consumer)) { RequeueCount = -1, DontAckDelay = TimeSpan.Zero };

        //Act
        var exception = Record.Exception(pump.Run);

        //Assert
        Assert.Null(exception);
        Assert.Equal(MessagePumpStatus.MP_STOPPED, pump.Status);
        Assert.Equal(new[] { deferred.Id, subsequent.Id }, handled);
        Assert.Equal(deferred.Id, Assert.Single(consumer.RequeueAttempts));
        Assert.Equal(subsequent.Id, Assert.Single(consumer.Acknowledged));
        Assert.Empty(consumer.Rejected);
        Assert.Equal(deferred.Id, Assert.Single(consumer.NegativelyAcknowledged));
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), entry =>
            entry.Level == LogEventLevel.Error
            && ReferenceEquals(entry.Exception, consumer.RequeueFailure)
            && entry.RenderMessage().Contains(deferred.Id.Value)
            && entry.RenderMessage().Contains(topic.Value));
        if (failNack)
        {
            Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(), entry =>
                entry.Level == LogEventLevel.Error
                && ReferenceEquals(entry.Exception, consumer.NackFailure)
                && entry.RenderMessage().Contains(deferred.Id.Value));
        }
    }
}
