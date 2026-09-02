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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Core.Tests.Confirmation.TestDoubles;

/// <summary>
/// Mimics a broker-backed producer's confirmation surface: it always reports
/// <see cref="ISupportPublishConfirmationAsync.UseAsyncPublishConfirmation"/> as true (as the RMQ and
/// Kafka producers do) and awaits the async subscribers when a confirmation is raised, so a test can
/// observe whether the mediator's full callback (including the Outbox mark-dispatched) was awaited.
/// </summary>
internal sealed class StubConfirmingProducerAsync(RoutingKey topic) : IAmAMessageProducerAsync, ISupportPublishConfirmation, ISupportPublishConfirmationAsync
{
    private event Func<PublishConfirmationResult, Task>? _onMessagePublishedAsync;

    public event Action<PublishConfirmationResult>? OnMessagePublished;

    public bool UseAsyncPublishConfirmation => true;

    event Func<PublishConfirmationResult, Task> ISupportPublishConfirmationAsync.OnMessagePublishedAsync
    {
        add => _onMessagePublishedAsync += value;
        remove => _onMessagePublishedAsync -= value;
    }

    public Publication Publication { get; } = new() { Topic = topic };

    public Activity? Span { get; set; }

    public IAmAMessageScheduler? Scheduler { get; set; }

    public bool SyncCallbackSubscribed => OnMessagePublished is not null;

    public Task SendAsync(Message message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SendWithDelayAsync(Message message, TimeSpan? delay, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task RaiseConfirmationAsync(PublishConfirmationResult result)
    {
        OnMessagePublished?.Invoke(result);
        await _onMessagePublishedAsync.InvokeAllAsync(result);
    }

    public ValueTask DisposeAsync() => default;
}
