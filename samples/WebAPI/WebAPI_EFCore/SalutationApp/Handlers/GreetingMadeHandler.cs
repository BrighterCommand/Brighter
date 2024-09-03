using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationApp.Entities;
using SalutationApp.EntityGateway;
using SalutationApp.Requests;

namespace SalutationApp.Handlers
{
    public class GreetingMadeHandlerAsync : RequestHandlerAsync<GreetingMade>
    {
        private readonly SalutationsEntityGateway _uow;
        private readonly IAmATransactionConnectionProvider _provider;
        private readonly IAmACommandProcessor _postBox;

        public GreetingMadeHandlerAsync(SalutationsEntityGateway uow,
            IAmATransactionConnectionProvider provider,
            IAmACommandProcessor postBox)
        {
            _uow = uow;
            _provider = provider;
            _postBox = postBox;
        }

        [UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] 
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICY_ASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default)
        {
            var posts = new List<string>();
            
            var tx = await _provider.GetTransactionAsync(cancellationToken);
            try
            {
                var salutation = new Salutation(@event.Greeting);

                _uow.Salutations.Add(salutation);

                posts.Add(await _postBox.DepositPostAsync(
                    new SalutationReceived(DateTimeOffset.Now),
                    _provider, cancellationToken: cancellationToken)
                );

                await _uow.SaveChangesAsync(cancellationToken);

                await _provider.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);

                await _provider.RollbackAsync(cancellationToken);

                Console.WriteLine("Salutation analytical record not saved");

                throw;
            }
            finally
            {
                _provider.Close();
            }

            _postBox.ClearOutbox(posts.ToArray());
            
            return await base.HandleAsync(@event,cancellationToken);
        }
    }
}
