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
using Paramore.Brighter.MessagingGateway.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway.Provisioning;

/// <summary>
/// OnMissingChannel.Assume means "the queue table is managed outside this application", so the
/// gateway must not go near the database. These tests prove that by pointing the configuration at
/// a server that cannot exist and asserting nothing throws.
///
/// An absence on its own proves nothing — a provisioner that did nothing at all would pass too.
/// Each Assume fact is therefore paired with a Create fact on the SAME unreachable configuration,
/// which must throw. The pair is what makes the silence meaningful.
/// </summary>
public class MsSqlQueueProvisioningAssumeTests
{
    // A port nothing listens on, and a one second timeout so a failure is quick rather than hung.
    private const string UnreachableConnectionString =
        "Server=127.0.0.1,10;Database=NoSuchDatabase;User Id=sa;Password=Password123!;Connect Timeout=1;Encrypt=false";

    private readonly RelationalDatabaseConfiguration _configuration = new(
        UnreachableConnectionString,
        queueStoreTable: "QueueThatIsManagedElsewhere");

    [Fact]
    public void When_the_subscription_assumes_the_queue_exists_should_not_open_a_connection()
    {
        //Arrange
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        var subscription = new MsSqlSubscription<MyCommand>(
            new SubscriptionName("assume.subscription"),
            new ChannelName("assume.channel"),
            new RoutingKey("assume.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Assume);

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(subscription));

        //Assert
        Assert.Null(exception);
    }

    [Fact]
    public void When_the_subscription_creates_a_missing_queue_against_an_unreachable_server_should_throw()
    {
        //Arrange -- the control for the fact above: the same configuration, the only difference
        //being that this one is allowed to touch the database.
        var channelFactory = new ChannelFactory(new MsSqlMessageConsumerFactory(_configuration));
        var subscription = new MsSqlSubscription<MyCommand>(
            new SubscriptionName("create.subscription"),
            new ChannelName("create.channel"),
            new RoutingKey("create.topic"),
            messagePumpType: MessagePumpType.Reactor,
            makeChannels: OnMissingChannel.Create);

        //Act
        var exception = Record.Exception(() => channelFactory.CreateSyncChannel(subscription));

        //Assert
        Assert.NotNull(exception);
    }

    [Fact]
    public void When_the_publication_assumes_the_queue_exists_should_not_open_a_connection()
    {
        //Arrange
        var publication = new Publication
        {
            Topic = new RoutingKey("assume.topic"), MakeChannels = OnMissingChannel.Assume
        };
        var producerFactory = new MsSqlMessageProducerFactory(
            _configuration, new List<Publication> { publication });

        //Act
        var exception = Record.Exception(() => producerFactory.Create());

        //Assert
        Assert.Null(exception);
    }

    [Fact]
    public void When_the_publication_creates_a_missing_queue_against_an_unreachable_server_should_throw()
    {
        //Arrange -- the control for the fact above.
        var publication = new Publication
        {
            Topic = new RoutingKey("create.topic"), MakeChannels = OnMissingChannel.Create
        };
        var producerFactory = new MsSqlMessageProducerFactory(
            _configuration, new List<Publication> { publication });

        //Act
        var exception = Record.Exception(() => producerFactory.Create());

        //Assert
        Assert.NotNull(exception);
    }

    private class MyCommand : Command
    {
        public MyCommand() : base(Guid.NewGuid()) { }
    }
}
