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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

public class ConfirmCountingRmqProducer(RmqMessagingGatewayConnection connection)
    : RmqMessageProducer(connection)
{
    private readonly List<ConfirmCountingRmqChannel> _channels = [];

    // Every channel the producer has opened, oldest first, so a test can inspect one it has replaced
    public IReadOnlyList<ConfirmCountingRmqChannel> Channels => _channels;

    protected override async Task ConnectToBrokerAsync(OnMissingChannel makeExchange, CancellationToken cancellationToken = default)
    {
        await base.ConnectToBrokerAsync(makeExchange, cancellationToken);
        if (Channel is ConfirmCountingRmqChannel) return;

        var counting = ConfirmCountingRmqChannel.Wrap(Channel!);
        _channels.Add(counting);
        Channel = (IChannel)(object)counting;
    }
}
