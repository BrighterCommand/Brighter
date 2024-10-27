using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationApp.Entities;
using SalutationApp.Requests;

namespace SalutationApp.Handlers
{
    public class GreetingMadeHandlerAsync : RequestHandlerAsync<GreetingMade>
    {
        private readonly IAmADynamoDbTransactionProvider _transactionProvider;
        private readonly IAmADynamoDbTransactionProvider _transactionProvider1;
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<GreetingMadeHandlerAsync> _logger;

        public GreetingMadeHandlerAsync(IAmADynamoDbTransactionProvider transactionProvider,
            IAmACommandProcessor postBox,
            ILogger<GreetingMadeHandlerAsync> logger)
        {
            _transactionProvider1 = transactionProvider;
            _postBox = postBox;
            _logger = logger;
            _transactionProvider = transactionProvider;
        }

        [UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] 
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICY_ASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default)
        {

            var posts = new List<string>();
            var context = new DynamoDBContext(_transactionProvider1.DynamoDb);
            var tx = await _transactionProvider1.GetTransactionAsync(cancellationToken);

            try
            {
                var salutation = new Salutation { Greeting = @event.Greeting };
                var attributes = context.ToDocument(salutation).ToAttributeMap();

                tx.TransactItems.Add(new TransactWriteItem
                {
                    Put = new Put { TableName = "Salutations", Item = attributes }
                });

                posts.Add(await _postBox.DepositPostAsync(new SalutationReceived(DateTimeOffset.Now), _transactionProvider, cancellationToken: cancellationToken));

                await _transactionProvider.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not save salutation");
                await _transactionProvider.RollbackAsync(cancellationToken);

                return await base.HandleAsync(@event, cancellationToken);
            }
            finally
            {
                _transactionProvider.Close();
            }

            await _postBox.ClearOutboxAsync(posts, cancellationToken: cancellationToken);
            
            return await base.HandleAsync(@event,cancellationToken);
        }
    }
}
