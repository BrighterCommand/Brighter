#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;
using StreamChannel = System.Threading.Channels.Channel;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

/// <summary>
/// #4505 — the token the <c>SubscriberClient</c> passes to <see cref="BrighterStreamHandler.HandleMessage"/> lives as
/// long as the client. Once a message is settled and the handler has replied, nothing registered on that token may
/// still reference it, or every message the client ever delivers stays in memory until the client stops.
/// </summary>
[Trait("Category", "GcpPubSub")]
public class GcpStreamSettledMessageRetentionTests
{
    private const int SettledMessages = 100;

    [Fact]
    public async Task When_a_gcp_stream_message_is_settled_should_not_be_kept_alive_by_the_handler_token()
    {
        // Arrange — a handler fed by a token whose source outlives every message, as the client's does
        var buffer = StreamChannel.CreateUnbounded<GcpStreamMessage>();
        var handler = new BrighterStreamHandler(buffer.Writer);
        using var clientLifetime = new CancellationTokenSource();

        // Act — deliver and settle messages while the token's source stays alive
        var settled = new List<WeakReference>();
        for (var i = 0; i < SettledMessages; i++)
        {
            settled.Add(await DeliverAndAcknowledge(handler, buffer, clientLifetime.Token));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert — no settled message is still reachable
        Assert.Equal(0, settled.Count(message => message.IsAlive));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference> DeliverAndAcknowledge(
        BrighterStreamHandler handler,
        System.Threading.Channels.Channel<GcpStreamMessage> buffer,
        CancellationToken clientLifetime)
    {
        var reply = handler.HandleMessage(new PubsubMessage { Data = ByteString.CopyFromUtf8("payload") }, clientLifetime);

        var delivered = await buffer.Reader.ReadAsync();
        delivered.Accepted();
        Assert.Equal(SubscriberClient.Reply.Ack, await reply);

        return new WeakReference(delivered);
    }
}
