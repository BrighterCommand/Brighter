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

using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

internal sealed class InMemoryReceiveHealthChannelFactory(InMemoryReceiveHealthConsumer consumer) : IAmAChannelFactory
{
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
        => new Channel(subscription.ChannelName, subscription.RoutingKey, consumer);

    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
        => new ChannelAsync(subscription.ChannelName, subscription.RoutingKey, consumer);

    public Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription,
        CancellationToken cancellationToken = default)
        => Task.FromResult(CreateAsyncChannel(subscription));
}
