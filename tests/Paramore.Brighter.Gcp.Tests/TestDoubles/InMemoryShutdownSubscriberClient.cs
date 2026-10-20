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
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;

namespace Paramore.Brighter.Gcp.Tests.TestDoubles;

internal sealed class InMemoryShutdownSubscriberClient(Exception? stopFailure, Exception? disposalFailure) : SubscriberClient
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int StopCount { get; private set; }
    public int DisposeCount { get; private set; }
    public ShutdownMode? ShutdownModeUsed { get; private set; }
    public bool StopTokenCanBeCanceled { get; private set; }

    public override Google.Cloud.PubSub.V1.SubscriptionName SubscriptionName =>
        Google.Cloud.PubSub.V1.SubscriptionName.FromProjectSubscription("shutdown-tests", "subscription");

    public override Task StartAsync(SubscriptionHandler handler) => _completion.Task;

    public override Task StopAsync(ShutdownOptions shutdownOptions, CancellationToken cancellationToken = default)
    {
        StopCount++;
        ShutdownModeUsed = shutdownOptions.Mode;
        StopTokenCanBeCanceled = cancellationToken.CanBeCanceled;
        if (stopFailure == null)
            _completion.TrySetResult();
        else
            _completion.TrySetException(stopFailure);
        return _completion.Task;
    }

    public override ValueTask DisposeAsync()
    {
        DisposeCount++;
        return disposalFailure == null ? default : new ValueTask(Task.FromException(disposalFailure));
    }
}
