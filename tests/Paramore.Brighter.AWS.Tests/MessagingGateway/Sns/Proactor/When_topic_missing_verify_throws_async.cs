using System;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;
using Xunit.Sdk;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway.Sns.Proactor;

public partial class SnsProactorTests
{
    [Fact]
    public async Task When_topic_missing_verify_throws_async()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Publication.MakeChannels = OnMissingChannel.Validate;
        
        
        // act & assert
        try
        {
            Producer = await CreateProducerAsync(Publication);
            Assert.Fail("Expecting an exception");
        }
        catch (Exception e) when(e is not XunitException)
        {
            Assert.IsType<BrokerUnreachableException>(e);
        }
    }   
}
