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
using SalutationApp.Entities;
using SalutationApp.Requests;

namespace SalutationApp.Handlers
{
    public class GreetingMadeHandlerAsync : RequestHandlerAsync<GreetingMade>
    {
        private readonly IAmABoxTransactionProvider<DbTransaction> _transactionConnectionProvider;
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<GreetingMadeHandlerAsync> _logger;

        public GreetingMadeHandlerAsync(IAmABoxTransactionProvider<DbTransaction> transactionConnectionProvider,
            IAmACommandProcessor postBox,
            ILogger<GreetingMadeHandlerAsync> logger)
        {
            _transactionConnectionProvider = transactionConnectionProvider;
            _postBox = postBox;
            _logger = logger;
        }

        [UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] 
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default)
        {
            var posts = new List<string>();
            
            var tx = await _transactionConnectionProvider.GetTransactionAsync(cancellationToken);
            var conn = tx.Connection; 
            try
            {
                var salutation = new Salutation(@event.Greeting);
                
               await conn.ExecuteAsync(
                   "insert into Salutation (greeting) values (@greeting)", 
                   new {greeting = salutation.Greeting}, 
                   tx); 
                
                posts.Add(await _postBox.DepositPostAsync(new SalutationReceived(DateTimeOffset.Now), _transactionConnectionProvider, cancellationToken: cancellationToken));
                
                await _transactionConnectionProvider.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not save salutation");
                
                //if it went wrong rollback entity write and Outbox write
                await _transactionConnectionProvider.RollbackAsync(cancellationToken);
                
                return await base.HandleAsync(@event, cancellationToken);
            }

            _postBox.ClearOutbox(posts.ToArray());
            
            return await base.HandleAsync(@event, cancellationToken);
        }
    }
}
