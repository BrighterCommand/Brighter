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
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.Provisioning;

/// <summary>
/// The gateway carries two ladders, sync and async, duplicated method for method, and duplicated
/// ladders drift. Every other provisioning test drives the sync one, so these drive the async one
/// through the same three cases — Create, Validate, and the guards that run before any connection.
///
/// The producer's async path matters on its own account: it is what the producer registry calls, so
/// it is the one an application actually takes, and it spent this PR's first draft wrapping the
/// synchronous body in a completed task.
/// </summary>
[Collection("MsSqlQueueProvisioning")]
public class MsSqlQueueProvisioningAsyncTests : IDisposable
{
    private readonly string _queueTable = MsSqlQueueProvisioningCreateTests.UniqueQueueTableName();
    private readonly RelationalDatabaseConfiguration _configuration;

    public MsSqlQueueProvisioningAsyncTests()
    {
        Configuration.EnsureDatabaseExists(Configuration.DefaultConnectingString);
        _configuration = new RelationalDatabaseConfiguration(
            Configuration.DefaultConnectingString, queueStoreTable: _queueTable);
    }

    [Fact]
    public async Task When_the_async_channel_creates_a_missing_queue_should_create_the_table_and_index()
    {
        //Arrange
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));

        //Act
        using var channel = await channelFactory.CreateAsyncChannelAsync(Subscription(OnMissingChannel.Create));

        //Assert
        Assert.NotNull(channel);
        Assert.True(MsSqlQueueProvisioningCreateTests.QueueTableExists(_queueTable));
        Assert.True(MsSqlQueueProvisioningCreateTests.TopicIndexExists(_queueTable));
    }

    [Fact]
    public async Task When_the_async_publication_creates_a_missing_queue_should_create_the_table_and_index()
    {
        //Arrange -- CreateAsync is the path MsSqlProducerRegistryFactory takes, and the one the
        //existing requeue and send tests reach the gateway through.
        var producerFactory = new MsSqlMessageProducerFactory(
            _configuration,
            new List<Publication>
            {
                new() { Topic = new RoutingKey("create.topic"), MakeChannels = OnMissingChannel.Create }
            });

        //Act
        var producers = await producerFactory.CreateAsync();

        //Assert
        Assert.Single(producers);
        Assert.True(MsSqlQueueProvisioningCreateTests.QueueTableExists(_queueTable));
        Assert.True(MsSqlQueueProvisioningCreateTests.TopicIndexExists(_queueTable));
    }

    [Fact]
    public async Task When_validating_a_missing_queue_asynchronously_should_throw_a_configuration_exception()
    {
        //Arrange -- nothing has created _queueTable.
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));

        //Act
        var exception = await Record.ExceptionAsync(
            () => channelFactory.CreateAsyncChannelAsync(Subscription(OnMissingChannel.Validate)));

        //Assert
        var configurationException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains(_queueTable, configurationException.Message);
    }

    [Fact]
    public async Task When_validating_an_existing_queue_asynchronously_should_not_throw()
    {
        //Arrange -- the control for the fact above, on the async ladder: same call, same name, the
        //only difference being that the table now exists.
        var creating = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        using (await creating.CreateAsyncChannelAsync(Subscription(OnMissingChannel.Create))) { }
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));

        //Act
        var exception = await Record.ExceptionAsync(
            () => channelFactory.CreateAsyncChannelAsync(Subscription(OnMissingChannel.Validate)));

        //Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task When_the_queue_table_name_would_close_the_ddl_bracket_should_throw_before_connecting_asynchronously()
    {
        //Arrange -- the unreachable server again, so that "before connecting" is measured and not
        //merely claimed. Both name guards run before ConnectAsync on this ladder too.
        var configuration = new RelationalDatabaseConfiguration(
            MsSqlQueueProvisioningAssumeTests.UnreachableConnectionString,
            queueStoreTable: "Queue]; DROP TABLE Users--");
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = await Record.ExceptionAsync(
            () => channelFactory.CreateAsyncChannelAsync(Subscription(OnMissingChannel.Create)));

        //Assert -- on the message, not the type: ConnectAsync wraps its own failure in a
        //ConfigurationException too, so the type check alone passes with the guard deleted.
        var configurationException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains("close the bracket", configurationException.Message);
        Assert.DoesNotContain("provider said", configurationException.Message);
    }

    [Fact]
    public async Task When_the_queue_table_name_leaves_no_room_for_the_index_name_should_throw_asynchronously()
    {
        //Arrange -- 120 characters, as on the sync ladder.
        var configuration = new RelationalDatabaseConfiguration(
            MsSqlQueueProvisioningAssumeTests.UnreachableConnectionString,
            queueStoreTable: new string('Q', 120));
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = await Record.ExceptionAsync(
            () => channelFactory.CreateAsyncChannelAsync(Subscription(OnMissingChannel.Create)));

        //Assert
        var configurationException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains("119", configurationException.Message);
    }

    [Fact]
    public async Task When_the_async_subscription_assumes_the_queue_exists_should_not_open_a_connection()
    {
        //Arrange -- the unreachable server is the instrument: reaching it at all would throw.
        var configuration = new RelationalDatabaseConfiguration(
            MsSqlQueueProvisioningAssumeTests.UnreachableConnectionString,
            queueStoreTable: "QueueThatIsManagedElsewhere");
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = await Record.ExceptionAsync(
            () => channelFactory.CreateAsyncChannelAsync(Subscription(OnMissingChannel.Assume)));

        //Assert -- paired with the Create fact above it, which throws on the same configuration.
        Assert.Null(exception);
    }

    [Fact]
    public async Task When_the_async_subscription_creates_against_an_unreachable_server_should_throw()
    {
        //Arrange -- the control for the fact above.
        var configuration = new RelationalDatabaseConfiguration(
            MsSqlQueueProvisioningAssumeTests.UnreachableConnectionString,
            queueStoreTable: "QueueThatIsManagedElsewhere");
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = await Record.ExceptionAsync(
            () => channelFactory.CreateAsyncChannelAsync(Subscription(OnMissingChannel.Create)));

        //Assert
        var configurationException = Assert.IsType<ConfigurationException>(exception);
        Assert.Contains("QueueThatIsManagedElsewhere", configurationException.Message);
    }

    private static MsSqlSubscription<MyCommand> Subscription(OnMissingChannel makeChannels) =>
        new(new SubscriptionName("async.subscription"),
            new ChannelName("async.channel"),
            new RoutingKey("async.topic"),
            messagePumpType: MessagePumpType.Proactor,
            makeChannels: makeChannels);

    public void Dispose() => MsSqlQueueProvisioningCreateTests.DropQueueTable(_queueTable);

    private class MyCommand : Command
    {
        public MyCommand() : base(Guid.NewGuid()) { }
    }
}
