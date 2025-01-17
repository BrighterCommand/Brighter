using System.Threading.Tasks;

namespace Paramore.Brighter;

public interface IAmASchedulerMessageConsumerAsync : IAmASchedulerMessageConsumer
{
    Task ConsumeAsync(Message message, RequestContext context);
}
