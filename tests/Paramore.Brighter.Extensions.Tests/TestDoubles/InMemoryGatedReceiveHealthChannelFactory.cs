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
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// Decorates a channel factory so that channel creation blocks until <see cref="Release"/> is called.
/// </summary>
/// <param name="inner">The factory that creates the channels once the gate opens.</param>
internal sealed class InMemoryGatedReceiveHealthChannelFactory(IAmAChannelFactory inner)
    : IAmAChannelFactory, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(false);

    /// <summary>
    /// Completed when the first creation reaches the gate.
    /// </summary>
    public TaskCompletionSource<bool> CreationBlocked { get; private set; } = NewCreationBlocked();

    /// <summary>
    /// Closes the gate again and re-arms <see cref="CreationBlocked"/>, so a later creation blocks.
    /// </summary>
    public void Reset()
    {
        CreationBlocked = NewCreationBlocked();
        _gate.Reset();
    }

    /// <summary>
    /// Opens the gate, letting blocked and later creations proceed.
    /// </summary>
    public void Release() => _gate.Set();

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <inheritdoc />
    public IAmAChannelSync CreateSyncChannel(Subscription subscription)
    {
        WaitForGate();
        return inner.CreateSyncChannel(subscription);
    }

    /// <inheritdoc />
    public IAmAChannelAsync CreateAsyncChannel(Subscription subscription)
    {
        WaitForGate();
        return inner.CreateAsyncChannel(subscription);
    }

    /// <inheritdoc />
    public async Task<IAmAChannelAsync> CreateAsyncChannelAsync(Subscription subscription,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(WaitForGate, cancellationToken);
        return await inner.CreateAsyncChannelAsync(subscription, cancellationToken);
    }

    private static TaskCompletionSource<bool> NewCreationBlocked()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void WaitForGate()
    {
        CreationBlocked.TrySetResult(true);
        if (!_gate.Wait(TimeSpan.FromSeconds(10)))
            throw new TimeoutException("Channel creation was not released.");
    }
}
