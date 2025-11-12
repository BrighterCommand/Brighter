using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Proactor;

public abstract partial class MessagingGatewayProactorTests<TPublication, TSubscription> 
{
    [Fact]
    public async Task When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Producer = await CreateProducerAsync(Publication);
        
        // Act
        var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
        await Parallel.ForEachAsync(Enumerable.Range(0, 10), options, async (_, ct) =>
        {
            var message = CreateMessage(Publication.Topic!);
            await Producer.SendAsync(message, ct);
        });
        
        // Assert
        Assert.True(true);
    }
}
