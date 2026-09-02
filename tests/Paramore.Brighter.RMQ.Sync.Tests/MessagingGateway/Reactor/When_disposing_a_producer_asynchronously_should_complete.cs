#region Licence
/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst <30480171+thomhurst@users.noreply.github.com>

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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Sync;
using Xunit;

namespace Paramore.Brighter.RMQ.Sync.Tests.MessagingGateway.Reactor;

public class RmqMessageProducerDisposeAsyncTests
{
    [Fact]
    public async Task When_disposing_a_producer_asynchronously_should_complete()
    {
        // DisposeAsync previously returned a TaskCompletionSource task that was never completed,
        // so awaiting it hung forever; this pins the fix.
        var rmqConnection = new RmqMessagingGatewayConnection
        {
            AmpqUri = new AmqpUriSpecification(new Uri("amqp://guest:guest@localhost:5672/%2f")),
            Exchange = new Exchange("paramore.brighter.exchange")
        };
        var producer = new RmqMessageProducer(rmqConnection);

        await producer.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
