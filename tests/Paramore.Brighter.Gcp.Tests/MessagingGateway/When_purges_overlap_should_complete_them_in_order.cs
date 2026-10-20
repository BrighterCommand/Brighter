#region Licence

/* The MIT License (MIT)
Copyright © 2026 gabisonia

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
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

[Trait("Category", "GcpPubSub")]
[Trait("Category", "GcpPubSubPurge")]
[Trait("Requires", "PubSubEmulator")]
public class GcpPurgeSerializationTests
{
    [Theory]
    [InlineData(false, SubscriptionMode.Pull)]
    [InlineData(true, SubscriptionMode.Pull)]
    [InlineData(false, SubscriptionMode.Stream)]
    [InlineData(true, SubscriptionMode.Stream)]
    public async Task When_purges_overlap_should_complete_them_in_order(bool useAsync, SubscriptionMode mode)
    {
        //Arrange
        var faults = new GcpPurgeFaultInterceptor();
        using var context = new GcpPurgeTestContext(mode, faults, useAsync);
        using var releaseFirst = new ManualResetEventSlim();
        var firstReachedSeek = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        faults.BeforeCall = (method, _) =>
        {
            if (method != "Seek" || Interlocked.Increment(ref calls) != 1)
                return;
            firstReachedSeek.SetResult(true);
            if (!releaseFirst.Wait(TimeSpan.FromSeconds(20)))
                throw new TimeoutException("First purge was not released");
        };
        var first = Task.Run(() => context.PurgeAsync(useAsync));
        Task? second = null;
        try
        {
            await firstReachedSeek.Task.WaitAsync(TimeSpan.FromSeconds(10));

            //Act
            second = Task.Run(() => context.PurgeAsync(useAsync, peer: mode == SubscriptionMode.Stream));
            await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(1)));

            //Assert
            Assert.False(second.IsCompleted, "The second purge must wait for the first purge to finish.");
        }
        finally
        {
            releaseFirst.Set();
            await first.WaitAsync(TimeSpan.FromSeconds(20));
            if (second != null)
                await second.WaitAsync(TimeSpan.FromSeconds(20));
        }
        Assert.Equal(2, calls);
    }
}
