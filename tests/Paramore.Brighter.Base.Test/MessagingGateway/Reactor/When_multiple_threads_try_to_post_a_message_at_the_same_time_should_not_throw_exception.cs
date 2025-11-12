using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Paramore.Brighter.Base.Test.MessagingGateway.Reactor;

public abstract partial class MessagingGatewayReactorTests<TPublication, TSubscription> : IAsyncLifetime
{
    [Fact]
    public void When_multiple_threads_try_to_post_a_message_at_the_same_time_should_not_throw_exception()
    {
        // Arrange
        Publication = CreatePublication(GetOrCreateRoutingKey());
        Producer = CreateProducer(Publication);
        
        // Act
        var options = new ParallelOptions { MaxDegreeOfParallelism = 4 };
        Parallel.ForEach(Enumerable.Range(0, 10), options, (_, _) =>
        {
            var message = CreateMessage(Publication.Topic!);
            Producer.Send(message);
        });
        
        // Assert
        Assert.True(true);
    }
}
