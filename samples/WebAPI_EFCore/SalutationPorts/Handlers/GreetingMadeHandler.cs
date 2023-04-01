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
    public class GreetingMadeHandlerAsync : RequestHandlerAsync<GreetingMade>
    {
        private readonly SalutationsEntityGateway _uow;
        private readonly IAmACommandProcessor _postBox;

        public GreetingMadeHandlerAsync(SalutationsEntityGateway uow, IAmACommandProcessor postBox)
        {
            _uow = uow;
            _postBox = postBox;
        }

        //[UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] -- we are using a global inbox, so need to be explicit!!
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default(CancellationToken))
        {
            var posts = new List<Guid>();

            var tx = await _uow.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var salutation = new Salutation(@event.Greeting);

                _uow.Salutations.Add(salutation);

                posts.Add(await _postBox.DepositPostAsync(new SalutationReceived(DateTimeOffset.Now), cancellationToken: cancellationToken));

                await _uow.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                await tx.RollbackAsync(cancellationToken);

                Console.WriteLine("Salutation analytical record not saved");

                throw;
            }

            await _postBox.ClearOutboxAsync(posts, cancellationToken: cancellationToken);

            return await base.HandleAsync(@event, cancellationToken);
        }
    }
}
