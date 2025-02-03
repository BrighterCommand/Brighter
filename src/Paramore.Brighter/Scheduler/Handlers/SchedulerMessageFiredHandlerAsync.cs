using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.Scheduler.Handlers;

public class SchedulerMessageFiredHandlerAsync(
    IAmAnOutboxProducerMediator<Message, CommittableTransaction> mediator)
    : RequestHandlerAsync<SchedulerMessageFired>
{
    public override async Task<SchedulerMessageFired> HandleAsync(SchedulerMessageFired command,
        CancellationToken cancellationToken = default)
    {
        var context = new RequestContext();
        await mediator.AddToOutboxAsync(command.Message, context, cancellationToken: cancellationToken);
        await mediator.ClearOutboxAsync([command.Message.Id], context, cancellationToken: cancellationToken);
        return await base.HandleAsync(command, cancellationToken);
    }
}
