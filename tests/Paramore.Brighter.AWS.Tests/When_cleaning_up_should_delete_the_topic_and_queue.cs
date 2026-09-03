using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.SecurityToken.Model;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.MessagingGateway;
using Paramore.Brighter.AWS.Tests.TestDoubles;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests;

/// <summary>
/// Tearing down through a provider has to leave nothing behind in the account, whether or not the
/// teardown itself succeeded. These stand real infrastructure up and then go looking for it, so
/// they need AWS credentials.
/// </summary>
[Trait("Category", "AWS")]
[Trait("LiveAWS", "true")]
public class MessageGatewayProviderCleanUpTests : IAsyncLifetime
{
    private readonly SnsStandardMessageGatewayProvider _provider = new();
    private readonly AWSMessagingGatewayConnection _connection = GatewayFactory.CreateFactory();
    private readonly RoutingKey _routingKey;
    private readonly ChannelName _channelName;
    private string _topicArn = string.Empty;

    public MessageGatewayProviderCleanUpTests()
    {
        _routingKey = _provider.GetOrCreateRoutingKey();
        _channelName = _provider.GetOrCreateChannelName();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Reaps whatever the test did not. Safe to run after a test that already tore down: reaping
    /// is a single attempt, so the second call finds nothing left to do.
    /// </summary>
    public async Task DisposeAsync() => await _provider.CleanUpAsync(null, null, []);

    [Fact]
    public async Task When_cleaning_up_should_delete_the_topic_and_queue()
    {
        //arrange
        var (producer, channel) = await CreateInfrastructureAsync();

        //act
        await _provider.CleanUpAsync(producer, channel, []);

        //assert
        await AssertTopicAndQueueDeletedAsync();
    }

    [Fact]
    public async Task When_teardown_throws_should_still_delete_the_topic_and_queue()
    {
        //arrange
        var (producer, channel) = await CreateInfrastructureAsync();

        //act
        // A purge that throttles is how teardown fails in practice, and it fails before anything
        // has been deleted.
        var exception = await Catch.ExceptionAsync(
            () => _provider.CleanUpAsync(producer, new PurgeFailingChannelAsync(channel), []));

        //assert
        try
        {
            Assert.IsType<PurgeQueueInProgressException>(exception);
            await AssertTopicAndQueueDeletedAsync();
        }
        finally
        {
            // The purge threw before CleanUpAsync could dispose either of these.
            channel.Dispose();
            await producer.DisposeAsync();
        }
    }

    private async Task<(IAmAMessageProducerAsync Producer, IAmAChannelAsync Channel)> CreateInfrastructureAsync()
    {
        var publication = _provider.CreatePublication(_routingKey);
        var subscription = _provider.CreateSubscription(_routingKey, _channelName, OnMissingChannel.Create);

        var producer = await _provider.CreateProducerAsync(publication);
        var channel = await _provider.CreateChannelAsync(subscription);

        // Composed rather than looked up: FindTopicAsync pages ListTopics, which is eventually
        // consistent, so a topic created moments ago may not be listed yet.
        using var stsClient = new AWSClientFactory(_connection).CreateStsClient();
        var identity = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());

        _topicArn = new Arn
        {
            Partition = _connection.Region.PartitionName,
            Service = "sns",
            Region = _connection.Region.SystemName,
            AccountId = identity.Account,
            Resource = _routingKey.Value
        }.ToString();

        return (producer, channel);
    }

    private async Task AssertTopicAndQueueDeletedAsync()
    {
        using var sqsClient = new AWSClientFactory(_connection).CreateSqsClient();
        using var snsClient = new AWSClientFactory(_connection).CreateSnsClient();

        await AssertEventuallyThrowsAsync<QueueDoesNotExistException>(
            () => sqsClient.GetQueueUrlAsync(_channelName.Value));

        await AssertEventuallyThrowsAsync<NotFoundException>(
            () => snsClient.GetTopicAttributesAsync(_topicArn));
    }

    /// <summary>
    /// Deletion is eventually consistent — SQS documents a queue as remaining visible for up to
    /// sixty seconds after DeleteQueue — so a single call proves nothing either way.
    /// </summary>
    private static async Task AssertEventuallyThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);

        while (true)
        {
            var exception = await Catch.ExceptionAsync(action);
            if (exception is TException)
            {
                return;
            }

            if (DateTimeOffset.UtcNow > deadline)
            {
                //assert against whatever we saw last, so a failure reports it
                Assert.IsType<TException>(exception);
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
