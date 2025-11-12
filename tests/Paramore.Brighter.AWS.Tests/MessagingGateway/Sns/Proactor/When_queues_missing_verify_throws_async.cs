using System;
using System.Threading.Tasks;
using Amazon.SQS.Model;
using Xunit;
using Xunit.Sdk;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Proactor;

public partial class SnsProactorTests
{
    [Fact]
    public async Task When_queues_missing_verify_throws_async()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Subscription = CreateSubscription(Publication.Topic!,
            GetOrCreateChannelName(),
            OnMissingChannel.Validate);
        
        // act & assert
        try
        {
            // Ensure the topic is created
            Producer = await CreateProducerAsync(Publication);
            Channel = await CreateChannelAsync(Subscription);
            Assert.Fail("Expecting an exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<QueueDoesNotExistException>(e);
        }
    }
}
