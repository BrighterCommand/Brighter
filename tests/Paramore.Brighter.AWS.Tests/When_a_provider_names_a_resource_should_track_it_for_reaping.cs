using System;
using System.Linq;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.AWS.Tests.MessagingGateway;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests;

/// <summary>
/// Each provider registers the names it hands out with its reaper by hand, in its own file. The
/// likeliest regression is a name — or a whole provider — added without a Track call, which leaks
/// silently and shows up weeks later as a quota.
/// </summary>
/// <remarks>
/// The live teardown tests cover one provider of four, because standing real infrastructure up
/// for each would cost minutes. This covers all four and needs no credentials: naming a resource
/// and creating one are separate steps, and only the naming is under test.
/// </remarks>
[Trait("Category", "AWS")]
public class ProviderResourceTrackingTests
{
    public static TheoryData<string> Providers =>
        new() { "SnsStandard", "SnsFifo", "SqsStandard", "SqsFifo" };

    [Theory]
    [MemberData(nameof(Providers))]
    public void When_a_provider_names_a_resource_should_track_it_for_reaping(string provider)
    {
        //arrange
        var (reaper, subscription) = CreateSubscriptionWithDeadLetterQueue(provider);

        //act
        var tracked = reaper.PendingTopics.Concat(reaper.PendingQueues).ToArray();

        //assert
        // Asserted against the subscription rather than against the names the provider generated:
        // the subscription is the record of what will actually be created, so this also catches a
        // provider that tracks one name and then subscribes with another. That is not
        // hypothetical — both SQS providers deliberately discard the channel name they were given
        // and subscribe with the publication's queue instead, and this is what pins that as
        // intended rather than forgotten.
        Assert.Contains(subscription.RoutingKey.Value, tracked);
        Assert.Contains(subscription.ChannelName.Value, tracked);

        Assert.NotNull(subscription.DeadLetterRoutingKey);
        Assert.Contains(subscription.DeadLetterRoutingKey!.Value, tracked);
    }

    private static (AwsTestResourceReaper Reaper, SqsSubscription Subscription)
        CreateSubscriptionWithDeadLetterQueue(string provider)
    {
        switch (provider)
        {
            case "SnsStandard":
            {
                var sut = new SnsStandardMessageGatewayProvider();
                return (sut.Reaper, sut.CreateSubscription(
                    sut.GetOrCreateRoutingKey(),
                    sut.GetOrCreateChannelName(),
                    OnMissingChannel.Create,
                    setupDeadLetterQueue: true));
            }
            case "SnsFifo":
            {
                var sut = new SnsFifoMessageGatewayProvider();
                return (sut.Reaper, sut.CreateSubscription(
                    sut.GetOrCreateRoutingKey(),
                    sut.GetOrCreateChannelName(),
                    OnMissingChannel.Create,
                    setupDeadLetterQueue: true));
            }
            case "SqsStandard":
            {
                var sut = new SqsStandardMessageGatewayProvider();
                return (sut.Reaper, sut.CreateSubscription(
                    sut.GetOrCreateRoutingKey(),
                    sut.GetOrCreateChannelName(),
                    OnMissingChannel.Create,
                    setupDeadLetterQueue: true));
            }
            case "SqsFifo":
            {
                var sut = new SqsFifoMessageGatewayProvider();
                return (sut.Reaper, sut.CreateSubscription(
                    sut.GetOrCreateRoutingKey(),
                    sut.GetOrCreateChannelName(),
                    OnMissingChannel.Create,
                    setupDeadLetterQueue: true));
            }
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(provider), provider, "No such gateway provider");
        }
    }
}
