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
using System.Threading.Tasks;
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

    private readonly string _queueTable = MsSqlQueueProvisioningCreateTests.UniqueQueueTableName();
    private readonly RelationalDatabaseConfiguration _configuration;

    public MsSqlQueueProvisioningConcurrencyTests()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);
        _configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: _queueTable);
    }

    [Fact]
    public async Task When_two_starts_race_to_create_the_queue_should_converge_on_one_table()
    {
        //Arrange -- separate factories, as separate instances would have, all released together.
        var starts = Enumerable.Range(0, ConcurrentStarts)
            .Select(_ => new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration)))
            .ToArray();

        //Act
        var results = await Task.WhenAll(starts.Select(factory => Task.Run(() =>
            Record.Exception(() => factory.CreateSyncChannel(
                new MsSqlSubscription<MyCommand>(
                    new SubscriptionName("race.subscription"),
                    new ChannelName("race.channel"),
                    new RoutingKey("race.topic"),
                    messagePumpType: MessagePumpType.Reactor,
                    makeChannels: OnMissingChannel.Create))))));

        //Assert -- every start succeeds, and there is exactly one table at the end.
        Assert.All(results, Assert.Null);
        Assert.True(MsSqlQueueProvisioningCreateTests.QueueTableExists(_queueTable));
    }

    public void Dispose() => MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);

    private class MyCommand : Command
    {
        public MyCommand() : base(Guid.NewGuid()) { }
    }
}
