using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationEntities;
using SalutationPorts.EntityGateway;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandlerAsync(
        SalutationsEntityGateway uow,
        IAmATransactionConnectionProvider provider,
        IAmACommandProcessor postBox)
        : RequestHandlerAsync<GreetingMade>
    {
        [UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] 
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICY_ASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default)
        {
            var posts = new List<Guid>();
            
            var tx = await provider.GetTransactionAsync(cancellationToken);
            try
            {
                var salutation = new Salutation(@event.Greeting);

                uow.Salutations.Add(salutation);

                posts.Add(await postBox.DepositPostAsync(
                    new SalutationReceived(DateTimeOffset.Now),
                    provider, cancellationToken: cancellationToken)
                );

                await uow.SaveChangesAsync(cancellationToken);

                await provider.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                await provider.RollbackAsync(cancellationToken);

                Console.WriteLine("Salutation analytical record not saved");

                throw;
            }
            finally
            {
                provider.Close();
            }

            postBox.ClearOutbox(posts.ToArray());
            
            return await base.HandleAsync(@event,cancellationToken);
        }
    }
}
