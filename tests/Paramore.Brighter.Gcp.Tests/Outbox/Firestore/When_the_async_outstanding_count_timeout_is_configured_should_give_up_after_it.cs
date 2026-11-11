#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel <guilhermeaappel@gmail.com>

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
using System.Diagnostics;
using System.Threading.Tasks;
using Google.Api.Gax.Grpc;
using Grpc.Core;
using Paramore.Brighter.Gcp.Tests.Firestore;
using Paramore.Brighter.Gcp.Tests.TestDoubles;
using Paramore.Brighter.Outbox.Firestore;
using Xunit;

namespace Paramore.Brighter.Gcp.Tests.Outbox.Firestore;

[Trait("Category", "Firestore")]
public class ConfiguredOutstandingCountTimeoutAsyncTests : IDisposable
{
    private readonly HeldReplyConnections _connections = new();
    private readonly FirestoreOutbox _outbox;

    public ConfiguredOutstandingCountTimeoutAsyncTests()
    {
        var configuration = Configuration.CreateOutbox($"outbox_{Uuid.New():N}");
        configuration.OutstandingCountTimeout = TimeSpan.FromSeconds(3);
        var configure = configuration.Configure;
        configuration.Configure = builder =>
        {
            configure?.Invoke(builder);
            builder.GrpcAdapter = GrpcNetClientAdapter.Default.WithAdditionalOptions(options => options.HttpHandler = _connections.Handler);
        };
        _outbox = new FirestoreOutbox(configuration);
    }

    [Fact]
    public async Task When_the_async_outstanding_count_timeout_is_configured_should_give_up_after_it()
    {
        //Arrange
        await _outbox.GetAsync(Id.Random(), new RequestContext());
        _connections.HoldReplies();
        var stopwatch = Stopwatch.StartNew();

        //Act
        var exception = await Record.ExceptionAsync(() =>
            _outbox.GetOutstandingMessageCountAsync(TimeSpan.FromMinutes(5), new RequestContext(), 100, null));
        stopwatch.Stop();

        //Assert
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6));
        var rpcException = Assert.IsType<RpcException>(exception);
        Assert.Equal(StatusCode.DeadlineExceeded, rpcException.StatusCode);
    }

    public void Dispose()
    {
        _connections.Dispose();
    }
}
