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

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

public class AzureServiceBusRetryWithoutActivityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_retrying_without_an_activity_should_preserve_the_original_trace_context(bool synchronous)
    {
        // Arrange
        const string traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
        const string traceState = "vendor=value";
        await using var client = new InMemoryRetryServiceBusClient(ServiceBusModelFactory.ServiceBusReceivedMessage());
        var scheduler = new AzureServiceBusRequeueScheduler(client);
        var message = new Message(new MessageHeader(new Id("original"), new RoutingKey("orders"),
            MessageType.MT_COMMAND, traceParent: new TraceParent(traceParent), traceState: new TraceState(traceState)),
            new MessageBody("order"));
        var previousActivity = Activity.Current;
        Activity.Current = null;
        try
        {
            // Act
            if (synchronous) scheduler.Requeue(message, new ChannelName("queue"), TimeSpan.Zero);
            else await scheduler.RequeueAsync(message, new ChannelName("queue"), TimeSpan.Zero);

            // Assert
            var retry = Assert.Single(client.Scheduled).Message;
            Assert.True(retry.ApplicationProperties.TryGetValue("cloudevents:traceparent", out var propagatedParent));
            Assert.True(retry.ApplicationProperties.TryGetValue("cloudevents:tracestate", out var propagatedState));
            Assert.Equal(traceParent, propagatedParent);
            Assert.Equal(traceState, propagatedState);
            Assert.Equal(traceParent, message.Header.TraceParent!.Value);
            Assert.Equal(traceState, message.Header.TraceState!.Value);
            Assert.Equal("original", message.Id.Value);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }
}
