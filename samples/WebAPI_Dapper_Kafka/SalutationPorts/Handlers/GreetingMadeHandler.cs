using System;
using System.Collections.Generic;
using Dapper;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationEntities;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandler : RequestHandler<GreetingMade>
    {
        private readonly IAmATransactionConnectionProvider _transactionConnectionProvider;
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<GreetingMadeHandler> _logger;

        public GreetingMadeHandler(IAmATransactionConnectionProvider transactionConnectionProvider, IAmACommandProcessor postBox, ILogger<GreetingMadeHandler> logger)
        {
            _transactionConnectionProvider = transactionConnectionProvider;
            _postBox = postBox;
            _logger = logger;
        }

        //[UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] -- we are using a global inbox, so need to be explicit!!
        [RequestLogging(step: 1, timing: HandlerTiming.Before)]
        [UsePolicy(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICY)]
        public override GreetingMade Handle(GreetingMade @event)
        {
            var posts = new List<Guid>();
            
            var tx = _transactionConnectionProvider.GetTransaction();
            try
            {
                var salutation = new Salutation(@event.Greeting);
                
               _transactionConnectionProvider.GetConnection().Execute(
                   "insert into Salutation (greeting) values (@greeting)", 
                   new {greeting = salutation.Greeting}, 
                   tx); 
                
                posts.Add(_postBox.DepositPost(
                    new SalutationReceived(DateTimeOffset.Now), 
                    _transactionConnectionProvider));
                
                tx.Commit();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not save salutation");
                
                //if it went wrong rollback entity write and Outbox write
                tx.Rollback();
                
                return base.Handle(@event);
            }

            _postBox.ClearOutbox(posts.ToArray());
            
            return base.Handle(@event);
        }
    }
}
