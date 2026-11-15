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
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.MsSql;

namespace Paramore.Brighter.MSSQL.Tests.MessagingGateway;

public class MsSqlFanOutConfigurationTests
{
    private readonly RelationalDatabaseConfiguration _configuration = new("Server=127.0.0.1,1;Database=unused;User Id=unused;Password=unused;Connect Timeout=1;Encrypt=false", queueStoreTable: "queue");

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task When_a_subscriber_name_is_missing_should_fail_before_database_access(string? name)
    {
        //Arrange
        var publication = new MsSqlPublication { Topic = new("topic"), SubscriberChannels = [new(name!)] };

        //Act
        var exception = (await Assert.That(() => new MsSqlMessageProducer(_configuration, publication)).ThrowsExactly<ConfigurationException>());

        //Assert
        await Assert.That(exception.Message).Contains("subscriber");
    }

    [Test]
    public async Task When_no_subscribers_are_configured_should_fail_before_database_access()
    {
        //Arrange
        var publication = new MsSqlPublication { Topic = new("topic") };

        //Act
        var exception = (await Assert.That(() => new MsSqlMessageProducer(_configuration, publication)).ThrowsExactly<ConfigurationException>());

        //Assert
        await Assert.That(exception.Message).Contains("subscriber");
    }

    [Test]
    public async Task When_a_subscriber_is_null_should_fail_before_database_access()
    {
        //Arrange
        var publication = new MsSqlPublication { Topic = new("topic"), SubscriberChannels = [null!] };

        //Act
        var exception = (await Assert.That(() => new MsSqlMessageProducer(_configuration, publication)).ThrowsExactly<ConfigurationException>());

        //Assert
        await Assert.That(exception.Message).Contains("subscriber");
    }

    [Test]
    public async Task When_a_subscriber_name_exceeds_the_column_size_should_fail_before_database_access()
    {
        //Arrange
        var publication = new MsSqlPublication { Topic = new("topic"), SubscriberChannels = [new(new string('a', 256))] };

        //Act
        var exception = (await Assert.That(() => new MsSqlMessageProducer(_configuration, publication)).ThrowsExactly<ConfigurationException>());

        //Assert
        await Assert.That(exception.Message).Contains("255");
    }

    [Test]
    public async Task When_subscriber_count_exceeds_the_limit_should_fail_before_database_access()
    {
        //Arrange
        var publication = new MsSqlPublication { Topic = new("topic"), SubscriberChannels = Enumerable.Range(0, 1001).Select(i => new ChannelName($"subscriber{i}")).ToArray() };

        //Act
        var exception = (await Assert.That(() => new MsSqlMessageProducer(_configuration, publication)).ThrowsExactly<ConfigurationException>());

        //Assert
        await Assert.That(exception.Message).Contains("1000");
    }

    [Test]
    public async Task When_a_subscriber_has_the_logical_topic_name_should_reject_ambiguous_retry_routing()
    {
        //Arrange
        var publication = new MsSqlPublication { Topic = new("topic"), SubscriberChannels = [new("topic")] };

        //Act
        var exception = (await Assert.That(() => new MsSqlMessageProducer(_configuration, publication)).ThrowsExactly<ConfigurationException>());

        //Assert
        await Assert.That(exception.Message).Contains("logical topic");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_sending_a_different_logical_topic_should_fail_before_database_access(bool async)
    {
        //Arrange
        using var producer = new MsSqlMessageProducer(_configuration,
            new MsSqlPublication { Topic = new("topic"), SubscriberChannels = [new("subscriber")] });
        var message = new Message(new MessageHeader(Id.Random(), new RoutingKey("other"), MessageType.MT_EVENT), new MessageBody("payload"));

        //Act
        var exception = async
            ? await Assert.That(() => producer.SendAsync(message)).ThrowsExactly<ConfigurationException>()
            : (await Assert.That(() => producer.Send(message)).ThrowsExactly<ConfigurationException>());

        //Assert
        await Assert.That(exception.Message).Contains("topic");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task When_an_alias_subscription_requests_delayed_retries_without_a_targeted_scheduler_should_fail_at_startup(bool async)
    {
        //Arrange
        var subscription = new MsSqlSubscription(new("subscriber"), new("queue"), new("topic"),
            requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand), requeueDelay: TimeSpan.FromSeconds(1)) { IsIndependentSubscriber = true };
        var factory = new MsSqlMessageConsumerFactory(_configuration);

        //Act
        var exception = (await Assert.That(() =>
        {
            if (async)
                factory.CreateAsync(subscription);
            else
                factory.Create(subscription);
        }).ThrowsExactly<ConfigurationException>());

        //Assert
        await Assert.That(exception.Message).Contains("scheduler");
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task When_opening_a_channel_with_unsafe_delayed_retries_should_fail_before_provisioning(bool async, bool asyncCreation)
    {
        //Arrange
        var configuration = new RelationalDatabaseConfiguration("invalid-connection-string", queueStoreTable: "queue");
        var subscription = new MsSqlSubscription(new("subscriber"), new("queue"), new("topic"),
            requestType: typeof(Paramore.Brighter.MSSQL.Tests.TestDoubles.MyCommand),
            requeueDelay: TimeSpan.FromSeconds(1), makeChannels: OnMissingChannel.Create) { IsIndependentSubscriber = true };
        var factory = new ChannelFactory(new MsSqlMessageConsumerFactory(configuration));

        //Act
        var exception = await Assert.That(async () =>
        {
            if (asyncCreation)
            {
                using var channel = await factory.CreateAsyncChannelAsync(subscription);
            }
            else if (async)
            {
                using var channel = factory.CreateAsyncChannel(subscription);
            }
            else
            {
                using var channel = factory.CreateSyncChannel(subscription);
            }
        }).ThrowsExactly<ConfigurationException>();

        //Assert
        await Assert.That(exception.Message).Contains("scheduler");
    }
}
