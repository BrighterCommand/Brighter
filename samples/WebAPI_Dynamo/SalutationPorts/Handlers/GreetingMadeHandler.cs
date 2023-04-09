using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationEntities;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandlerAsync : RequestHandlerAsync<GreetingMade>
    {
        private readonly DynamoDbUnitOfWork  _uow;
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<GreetingMadeHandlerAsync> _logger;

        public GreetingMadeHandlerAsync(IAmABoxTransactionConnectionProvider uow, IAmACommandProcessor postBox, ILogger<GreetingMadeHandlerAsync> logger)
        {
            _uow = (DynamoDbUnitOfWork)uow;
            _postBox = postBox;
            _logger = logger;
        }

        //[UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] -- we are using a global inbox, so need to be explicit!!
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default)
        {
            var posts = new List<Guid>();
            var context = new DynamoDBContext(_uow.DynamoDb);
            var tx = _uow.BeginOrGetTransaction();
            try
            {
                var salutation = new Salutation{ Greeting = @event.Greeting};
                var attributes = context.ToDocument(salutation).ToAttributeMap();
                
                tx.TransactItems.Add(new TransactWriteItem{Put = new Put{ TableName = "Salutations", Item = attributes}});
                
                posts.Add(await _postBox.DepositPostAsync(new SalutationReceived(DateTimeOffset.Now), cancellationToken: cancellationToken));
                
                await _uow.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not save salutation");
                _uow.Rollback();
                
                return await base.HandleAsync(@event, cancellationToken);
            }

            await _postBox.ClearOutboxAsync(posts, cancellationToken: cancellationToken);
            
            return await base.HandleAsync(@event, cancellationToken);
        }
    }
}
