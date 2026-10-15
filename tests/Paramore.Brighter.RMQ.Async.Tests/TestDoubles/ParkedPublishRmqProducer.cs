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

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RMQ.Async;
using RabbitMQ.Client;

namespace Paramore.Brighter.RMQ.Async.Tests.TestDoubles;

// The first channel the producer opens fails its parked publish on release; every later channel publishes normally
public class ParkedPublishRmqProducer(RmqMessagingGatewayConnection connection)
    : RmqMessageProducer(connection)
{
    private readonly ConcurrentDictionary<int, TaskCompletionSource<ParkedPublishRmqChannel>> _opened = new();
    private int _channelsOpened;

    // Completes with the nth channel the producer opens (from zero), once it has opened it
    public Task<ParkedPublishRmqChannel> OpenedChannel(int index) => OpenedChannelSource(index).Task;

    protected override async Task ConnectToBrokerAsync(OnMissingChannel makeExchange, CancellationToken cancellationToken = default)
    {
        await base.ConnectToBrokerAsync(makeExchange, cancellationToken);
        if (Channel is ParkedPublishRmqChannel) return;

        var index = _channelsOpened++;
        var parked = ParkedPublishRmqChannel.Wrap(Channel!, failWhenReleased: index == 0);
        Channel = (IChannel)(object)parked;
        OpenedChannelSource(index).SetResult(parked);
    }

    private TaskCompletionSource<ParkedPublishRmqChannel> OpenedChannelSource(int index) =>
        _opened.GetOrAdd(index, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
}
