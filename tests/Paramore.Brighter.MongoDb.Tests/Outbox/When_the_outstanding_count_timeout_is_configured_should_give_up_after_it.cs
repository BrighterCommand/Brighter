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
using MongoDB.Driver;
using Paramore.Brighter.MongoDb.Tests.TestDoubles;
using Paramore.Brighter.Outbox.MongoDb;
using Xunit;

namespace Paramore.Brighter.MongoDb.Tests.Outbox;

[Trait("Category", "MongoDB")]
public class ConfiguredOutstandingCountTimeoutTests : IDisposable
{
    private readonly HeldReplyProxy _proxy;
    private readonly MongoClient _client;
    private readonly MongoDbOutbox _outbox;

    public ConfiguredOutstandingCountTimeoutTests()
    {
        var server = new MongoUrl(Const.ConnectionString).Server;
        _proxy = new HeldReplyProxy(server.Host, server.Port);
        _proxy.Start();
        _client = new MongoClient(new MongoUrlBuilder(Const.ConnectionString)
        {
            Server = new MongoServerAddress("127.0.0.1", _proxy.Port),
            DirectConnection = true
        }.ToMongoUrl());
        _outbox = new MongoDbOutbox(new MongoDbConfiguration(_client, Const.DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration { Name = $"Outbox{Uuid.New():N}" },
            OutstandingCountTimeout = TimeSpan.FromSeconds(3)
        });
    }

    [Fact]
    public void When_the_outstanding_count_timeout_is_configured_should_give_up_after_it()
    {
        //Arrange
        _outbox.GetOutstandingMessageCount(TimeSpan.FromMinutes(5), new RequestContext(), 100, null);
        _proxy.HoldReplies();
        var stopwatch = Stopwatch.StartNew();

        //Act
        var exception = Record.Exception(() =>
            _outbox.GetOutstandingMessageCount(TimeSpan.FromMinutes(5), new RequestContext(), 100, null));
        stopwatch.Stop();

        //Assert
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(6));
        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    public void Dispose()
    {
        _proxy.Dispose();
        var cluster = _client.Cluster;
        _client.Dispose();
        ClusterRegistry.Instance.UnregisterAndDisposeCluster(cluster);
    }
}
