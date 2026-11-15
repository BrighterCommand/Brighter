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
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Grpc.Core;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.MessagingGateway.Stream;

[Property("Category", "GcpPubSubStream")]
public class GcpStreamShutdownCleanupTests
{
    public static IEnumerable<object[]> Failures()
    {
        foreach (var useAsync in new[] { false, true })
        {
            yield return [useAsync, 1, 1];
            yield return [useAsync, 2, 2];
            yield return [useAsync, 3, 3];
            yield return [useAsync, 3, 1];
            yield return [useAsync, 1, 3];
            yield return [useAsync, 0, 1];
        }
    }

    [Test]
    [MethodDataSource(nameof(Failures))]
    public async Task When_stream_shutdown_fails_should_dispose_the_client_and_preserve_non_cancellation_errors(
        bool useAsync, int stopFailureKind, int disposalFailureKind)
    {
        // Arrange
        var stopFailure = CreateFailure(stopFailureKind);
        var disposalFailure = CreateFailure(disposalFailureKind);
        var client = new InMemoryShutdownSubscriberClient(stopFailure, disposalFailure);
        var stream = new GcpStreamConsumer(client);
        await Assert.That(stream.TryStart()).IsTrue();
        var consumer = new GcpPubSubStreamMessageConsumer(
            new GcpMessagingGatewayConnection(), stream, client.SubscriptionName, TimeProvider.System);

        // Act
        var error = await TestExceptionRecorder.CaptureAsync(() => useAsync
            ? consumer.DisposeAsync().AsTask()
            : Task.Run(consumer.Dispose));

        // Assert
        await Assert.That(client.StopCount).IsEqualTo(1);
        await Assert.That(client.DisposeCount).IsEqualTo(1);
        await Assert.That(client.ShutdownModeUsed).IsEqualTo(SubscriberClient.ShutdownMode.NackImmediately);
        await Assert.That(client.StopTokenCanBeCanceled).IsFalse();
        var expectedError = disposalFailure as RpcException ?? stopFailure as RpcException;
        await Assert.That(error).IsSameReferenceAs(expectedError);
        await Assert.That(stream.TryStart()).IsFalse();

        if (useAsync)
            await consumer.DisposeAsync();
        else
            consumer.Dispose();
        await Assert.That(client.StopCount).IsEqualTo(1);
        await Assert.That(client.DisposeCount).IsEqualTo(1);
    }

    private static Exception? CreateFailure(int kind) => kind switch
    {
        0 => null,
        1 => new TaskCanceledException("Subscriber shutdown canceled"),
        2 => new OperationCanceledException("Subscriber shutdown canceled"),
        3 => new RpcException(new Status(StatusCode.PermissionDenied, "Subscriber service failure")),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
