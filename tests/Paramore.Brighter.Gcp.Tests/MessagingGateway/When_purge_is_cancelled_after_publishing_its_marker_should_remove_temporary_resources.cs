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
public class GcpPurgeCancellationCleanupTests
{
    [Fact]
    public async Task When_purge_is_cancelled_after_publishing_its_marker_should_remove_temporary_resources()
    {
        //Arrange
        var faults = new GcpPurgeFaultInterceptor();
        using var context = new GcpPurgeTestContext(SubscriptionMode.Pull, faults, true);
        using var cancellation = new CancellationTokenSource();
        faults.AfterCall = (method, _) =>
        {
            if (method != "Publish")
                return;
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        };

        //Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.PurgeAsync(true, cancellation.Token));

        //Assert
        Assert.Empty(context.TemporarySubscriptions());
        Assert.Empty(context.TemporaryTopics());
    }
}
