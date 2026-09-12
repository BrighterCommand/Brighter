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
using System.Collections.Generic;
using System.Linq;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.Provisioning;

/// <summary>
/// The queue table is configuration-level: nothing in this assembly varies it per publication or
/// per subscription, so a producer factory holding twenty publications would otherwise open twenty
/// connections and run twenty identical no-op DDL batches as the host starts, and a dispatcher would
/// repeat the probe once per performer.
///
/// A gateway instance therefore remembers that it has provisioned. The scope of that memory is the
/// instance and nothing wider, which is what the second fact here pins: a new start still checks.
/// </summary>
[Collection("MsSqlQueueProvisioning")]
public class MsSqlQueueProvisioningOnceTests : IDisposable
{
    private readonly string _queueTable = MsSqlQueueProvisioningCreateTests.UniqueQueueTableName();
    private readonly RelationalDatabaseConfiguration _configuration;

    public MsSqlQueueProvisioningOnceTests()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);
        _configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: _queueTable);
    }

    [Fact]
    public void When_one_gateway_provisions_repeatedly_should_only_go_to_the_database_once()
    {
        //Arrange -- twenty publications naming one queue table, which is the shape that made this
        //worth doing. Dropping the table behind the factory's back is the instrument: whatever the
        //factory does after that is visible, and a factory still provisioning per publication would
        //put the table straight back.
        var publications = Enumerable.Range(0, 20)
            .Select(i => new Publication
            {
                Topic = new RoutingKey($"create.topic.{i}"), MakeChannels = OnMissingChannel.Create
            })
            .ToList();
        var producerFactory = new MsSqlMessageProducerFactory(_configuration, publications);

        //Act
        var producers = producerFactory.Create();
        Assert.Equal(20, producers.Count);
        Assert.True(MsSqlQueueProvisioningCreateTests.QueueTableExists(_queueTable));

        MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);
        producerFactory.Create();

        //Assert -- the second Create ran twenty publications and touched the database for none of
        //them. This is the cost of the memory as well as the point of it, and it is the right trade:
        //a table dropped under a running host is not a case provisioning can defend against anyway.
        Assert.False(MsSqlQueueProvisioningCreateTests.QueueTableExists(_queueTable));
    }

    [Fact]
    public void When_a_new_gateway_provisions_should_go_to_the_database_again()
    {
        //Arrange -- the control, and the one that matters for correctness: the memory is per
        //instance, so a restart, a second host, or any other new factory still provisions. Without
        //this the fact above would also pass on a gateway that had simply stopped provisioning.
        var publication = new Publication
        {
            Topic = new RoutingKey("create.topic"), MakeChannels = OnMissingChannel.Create
        };
        new MsSqlMessageProducerFactory(_configuration, new List<Publication> { publication }).Create();
        MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);

        //Act
        new MsSqlMessageProducerFactory(_configuration, new List<Publication> { publication }).Create();

        //Assert
        Assert.True(MsSqlQueueProvisioningCreateTests.QueueTableExists(_queueTable));
    }

    public void Dispose() => MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);
}
