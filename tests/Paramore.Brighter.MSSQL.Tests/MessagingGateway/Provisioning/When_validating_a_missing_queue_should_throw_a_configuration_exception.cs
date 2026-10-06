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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.Provisioning;

/// <summary>
/// OnMissingChannel.Validate is the production setting: the infrastructure is someone else's job,
/// but a missing queue table should fail at startup with a sentence that says so, rather than on
/// the first send with whatever SQL Server says about a missing object.
/// </summary>
[Collection("MsSqlQueueProvisioning")]
public class MsSqlQueueProvisioningValidateTests : IDisposable
{
    private readonly string _queueTable = MsSqlQueueProvisioningCreateTests.UniqueQueueTableName();
    private readonly RelationalDatabaseConfiguration _configuration;

    public MsSqlQueueProvisioningValidateTests()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);
        _configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: _queueTable);
    }

    [Fact]
    public void When_validating_a_missing_queue_should_throw_a_configuration_exception()
    {
        //Arrange -- nothing has created _queueTable.
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        var subscription = Subscription(OnMissingChannel.Validate);

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(subscription));

        //Assert
        Assert.IsType<ConfigurationException>(exception);
        Assert.Contains(_queueTable, exception!.Message);
    }

    [Fact]
    public void When_validating_an_existing_queue_should_not_throw()
    {
        //Arrange -- the control for the fact above: same call, same table name, the only difference
        //being that the table now exists. Without it, a Validate that threw unconditionally would
        //pass the test above.
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        using (var created = channelFactory.CreateSyncChannel(Subscription(OnMissingChannel.Create))) { }

        //Act -- a second factory, because a gateway instance remembers that it has provisioned and
        //the claim here is about Validate reaching the database and finding the table.
        var validating = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        var exception = Record.Exception(() =>
        {
            using var channel = validating.CreateSyncChannel(Subscription(OnMissingChannel.Validate));
        });

        //Assert
        Assert.Null(exception);
    }

    [Fact]
    public void When_validating_a_queue_too_long_to_have_been_created_here_should_not_throw()
    {
        //Arrange -- 125 characters. Create refuses that name, because the index derived from it
        //would be 134 and SQL Server's limit is 128; Validate builds no identifier at all, so a
        //table of that name that already exists is one this gateway can legitimately be pointed at.
        //Applying the Create bound to both would reject a working configuration.
        var longName = "Q" + new string('v', 124);
        CreateQueueTableDirectly(longName);
        var configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: longName);
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        try
        {
            //Act
            var exception = Record.Exception(() =>
            {
                using var channel = channelFactory.CreateSyncChannel(Subscription(OnMissingChannel.Validate));
            });

            //Assert
            Assert.Null(exception);
        }
        finally
        {
            MsSqlQueueProvisioningCreateTests.DropQueueTable(longName);
        }
    }

    private static void CreateQueueTableDirectly(string queueTable)
    {
        using var connection = new SqlConnection(Configuration.DefaultConnectingString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = MsSqlQueueBuilder.GetDDL(queueTable);
        command.ExecuteNonQuery();
    }

    private static MsSqlSubscription<MyCommand> Subscription(OnMissingChannel makeChannels) =>
        new(new SubscriptionName("validate.subscription"),
            new ChannelName("validate.channel"),
            new RoutingKey("validate.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: makeChannels);

    public void Dispose() => MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);

    private class MyCommand : Command
    {
        public MyCommand() : base(Guid.NewGuid()) { }
    }
}
