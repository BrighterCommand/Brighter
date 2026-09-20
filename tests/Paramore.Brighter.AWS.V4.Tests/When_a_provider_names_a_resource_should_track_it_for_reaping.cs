using System;
using System.Linq;
using Paramore.Brighter.AWS.V4.Tests.Helpers;
using Paramore.Brighter.AWS.V4.Tests.MessagingGateway;
using Paramore.Brighter.MessagingGateway.AWSSQS.V4;
using Xunit;

namespace Paramore.Brighter.AWS.V4.Tests;

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
        var (reaper, subscription) = CreateSubscriptionWithRejectionChannels(provider);

        //act
        var tracked = reaper.PendingTopics.Concat(reaper.PendingQueues).ToArray();

        //assert
        // Asserted against the subscription rather than against the names the provider generated:
        // the subscription is the record of what will actually be created, so this also catches a
        // provider that tracks one name and then subscribes with another. That is not
        // hypothetical — both SQS providers deliberately discard the channel name they were given
        // and subscribe with the publication's queue instead, and this is what pins that as
        // intended rather than forgotten. It also catches a provider that adapts a canonical
        // routing key to the transport's alphabet and then tracks the name it was handed rather
        // than the adapted one AWS will actually see.
        Assert.Contains(subscription.RoutingKey.Value, tracked);
        Assert.Contains(subscription.ChannelName.Value, tracked);

        Assert.NotNull(subscription.DeadLetterRoutingKey);
        Assert.Contains(subscription.DeadLetterRoutingKey!.Value, tracked);

        // The invalid-message queue is the resource the conformance suite adds, and the one the
        // reaper could not have known about: it is created lazily on the first rejection, so no
        // channel or producer this fixture holds is a handle on it.
        Assert.NotNull(subscription.InvalidMessageRoutingKey);
        Assert.Contains(subscription.InvalidMessageRoutingKey!.Value, tracked);
    }

    private static (AwsTestResourceReaper Reaper, SqsSubscription Subscription)
        CreateSubscriptionWithRejectionChannels(string provider)
    {
        switch (provider)
        {
            case "SnsStandard":
            {
                var sut = new SnsStandardMessageGatewayProvider();
                var routingKey = sut.GetOrCreateRoutingKey();
                return (sut.Reaper, sut.CreateSubscription(
                    routingKey,
                    sut.GetOrCreateChannelName(),
                    OnMissingChannel.Create,
                    deadLetterRoutingKey: new RoutingKey($"{routingKey}.DLQ"),
                    invalidMessageRoutingKey: new RoutingKey($"{routingKey}.Invalid")));
            }
            case "SnsFifo":
            {
                var sut = new SnsFifoMessageGatewayProvider();
                var routingKey = sut.GetOrCreateRoutingKey();
                return (sut.Reaper, sut.CreateSubscription(
                    routingKey,
                    sut.GetOrCreateChannelName(),
                    OnMissingChannel.Create,
                    deadLetterRoutingKey: new RoutingKey($"{routingKey}.DLQ"),
                    invalidMessageRoutingKey: new RoutingKey($"{routingKey}.Invalid")));
            }
            case "SqsStandard":
            {
                var sut = new SqsStandardMessageGatewayProvider();
                var routingKey = sut.GetOrCreateRoutingKey();
                return (sut.Reaper, sut.CreateSubscription(
                    routingKey,
                    sut.GetOrCreateChannelName(),
                    OnMissingChannel.Create,
                    deadLetterRoutingKey: new RoutingKey($"{routingKey}.DLQ"),
                    invalidMessageRoutingKey: new RoutingKey($"{routingKey}.Invalid")));
            }
            case "SqsFifo":
            {
                var sut = new SqsFifoMessageGatewayProvider();
                var routingKey = sut.GetOrCreateRoutingKey();
                return (sut.Reaper, sut.CreateSubscription(
                    routingKey,
                    sut.GetOrCreateChannelName(),
                    OnMissingChannel.Create,
                    deadLetterRoutingKey: new RoutingKey($"{routingKey}.DLQ"),
                    invalidMessageRoutingKey: new RoutingKey($"{routingKey}.Invalid")));
            }
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(provider), provider, "No such gateway provider");
        }
    }
}
