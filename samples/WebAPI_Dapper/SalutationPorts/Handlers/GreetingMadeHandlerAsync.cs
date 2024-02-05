using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationEntities;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandlerAsync(
        IAmABoxTransactionProvider<DbTransaction> transactionConnectionProvider,
        IAmACommandProcessor postBox,
        ILogger<GreetingMadeHandlerAsync> logger)
        : RequestHandlerAsync<GreetingMade>
    {
        [UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] 
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default)
        {
            var posts = new List<Guid>();
            
            var tx = await transactionConnectionProvider.GetTransactionAsync(cancellationToken);
            var conn = tx.Connection; 
            try
            {
                var salutation = new Salutation(@event.Greeting);
                
               await conn.ExecuteAsync(
                   "insert into Salutation (greeting) values (@greeting)", 
                   new {greeting = salutation.Greeting}, 
                   tx); 
                
                posts.Add(await postBox.DepositPostAsync(new SalutationReceived(DateTimeOffset.Now), transactionConnectionProvider, cancellationToken: cancellationToken));
                
                await transactionConnectionProvider.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not save salutation");
                
                //if it went wrong rollback entity write and Outbox write
                await transactionConnectionProvider.RollbackAsync(cancellationToken);
                
                return await base.HandleAsync(@event, cancellationToken);
            }

            postBox.ClearOutbox(posts.ToArray());
            
            return await base.HandleAsync(@event, cancellationToken);
        }
    }
}
