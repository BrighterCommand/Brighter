#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Linq;
using System.Threading;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.Provisioning;

/// <summary>
/// The case this whole mechanism exists for: a scaled-out consumer where every instance starts at
/// once against a database where the queue table does not yet exist. One CREATE TABLE wins and the
/// rest must recognise a lost race rather than fail the startup they are in the middle of.
///
/// This is the test five review rounds asked for and that the code could not have while it lived
/// in a sample, because no test project references samples/.
/// </summary>
[Collection("MsSqlQueueProvisioning")]
public class MsSqlQueueProvisioningConcurrencyTests : IDisposable
{
    private const int ConcurrentStarts = 8;

    //Generous, because these exist to turn a hang into a failure rather than to measure anything.
    private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(60);

    private readonly string _queueTable = MsSqlQueueProvisioningCreateTests.UniqueQueueTableName();
    private readonly RelationalDatabaseConfiguration _configuration;

    public MsSqlQueueProvisioningConcurrencyTests()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);
        _configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: _queueTable);
    }

    [Fact]
    public void When_two_starts_race_to_create_the_queue_should_converge_on_one_table()
    {
        //Arrange -- separate factories, as separate instances would have, all released together.
        //
        //Two things make "together" mean something. Dedicated threads rather than Task.Run, because
        //the thread pool injects threads at about one a second: eight queued work items need not be
        //running at once, and a race whose participants never overlap passes without ever reaching
        //2714 or 1205 — the paths this test exists to cover. And a barrier they all park at, after
        //a throwaway connection each has warmed the pool, so that what follows the release is the
        //DDL rather than eight staggered connection handshakes.
        var starts = Enumerable.Range(0, ConcurrentStarts)
            .Select(_ => new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration)))
            .ToArray();
        var outcomes = new Exception?[ConcurrentStarts];

        using var gate = new Barrier(ConcurrentStarts);
        var threads = new Thread[ConcurrentStarts];

        //Act
        for (var i = 0; i < ConcurrentStarts; i++)
        {
            var index = i;
            threads[index] = new Thread(() =>
            {
                //The whole body, warm-up included. An unhandled exception on a raw foreground
                //thread terminates the test host: a transient failure in the warm-up would read in
                //CI as a crashed run with no assertion message rather than as a failed test. Worse,
                //a thread that died before signalling would leave the other seven parked on the
                //barrier and Join() would never return, so the run would hang instead of failing.
                outcomes[index] = Record.Exception(() =>
                {
                    WarmTheConnectionPool();
                    if (!gate.SignalAndWait(GateTimeout))
                        throw new TimeoutException("The other starts never reached the gate.");

                    using var channel = starts[index].CreateSyncChannel(
                        new MsSqlSubscription<MyCommand>(
                            new SubscriptionName("race.subscription"),
                            new ChannelName("race.channel"),
                            new RoutingKey("race.topic"),
                            messagePumpType: MessagePumpType.Reactor,
                            makeChannels: OnMissingChannel.Create));
                });
            });
            threads[index].Start();
        }

        //Joined to completion before anything is asserted: an Assert inside this loop would exit
        //the method with the remaining threads still parked on the barrier, and `using var gate`
        //would then dispose it under them.
        var joined = threads.Select(thread => thread.Join(JoinTimeout)).ToArray();
        Assert.All(joined, finished => Assert.True(finished, "A start neither finished nor failed."));

        //Assert -- every start succeeds, and there is exactly one table at the end.
        Assert.All(outcomes, Assert.Null);
        Assert.True(MsSqlQueueProvisioningCreateTests.QueueTableExists(_queueTable));
    }

    private static void WarmTheConnectionPool()
    {
        using var connection = new SqlConnection(Configuration.DefaultConnectingString);
        connection.Open();
    }

    public void Dispose() => MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);

    private class MyCommand : Command
    {
        public MyCommand() : base(Guid.NewGuid()) { }
    }
}
