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

using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway;

[Trait("Category", "GcpPubSub")]
[Trait("Category", "GcpPubSubPurge")]
[Trait("Requires", "PubSubEmulator")]
public class GcpPurgeLostResponseTests
{
    [Theory]
    [InlineData(false, "CreateTopic")]
    [InlineData(true, "CreateTopic")]
    [InlineData(false, "CreateSubscription")]
    [InlineData(true, "CreateSubscription")]
    public async Task When_a_purge_create_response_is_lost_should_remove_temporary_resources(bool useAsync, string method)
    {
        //Arrange
        var faults = new GcpPurgeFaultInterceptor();
        using var context = new GcpPurgeTestContext(SubscriptionMode.Pull, faults, useAsync);
        var injected = 0;
        faults.AfterCall = (called, _) =>
        {
            if (called == method && Interlocked.Increment(ref injected) == 1)
            {
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Lost create response"));
            }
        };

        //Act
        var failure = await Assert.ThrowsAsync<RpcException>(() => context.PurgeAsync(useAsync));

        //Assert
        Assert.Equal(StatusCode.DeadlineExceeded, failure.StatusCode);
        Assert.Equal(1, injected);
        Assert.Empty(context.TemporarySubscriptions());
        Assert.Empty(context.TemporaryTopics());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task When_purge_marker_publication_response_is_lost_should_recover_and_remove_temporary_resources(bool useAsync)
    {
        //Arrange
        var faults = new GcpPurgeFaultInterceptor();
        using var context = new GcpPurgeTestContext(SubscriptionMode.Pull, faults, useAsync);
        var publications = 0;
        faults.AfterCall = (method, _) =>
        {
            if (method == "Publish" && Interlocked.Increment(ref publications) == 1)
            {
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Lost publication response"));
            }
        };

        //Act
        await context.PurgeAsync(useAsync);

        //Assert
        Assert.Equal(2, publications);
        Assert.Empty(context.TemporarySubscriptions());
        Assert.Empty(context.TemporaryTopics());
    }
}
