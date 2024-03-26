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
using SalutationEntities;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandlerAsync(
        IAmADynamoDbTransactionProvider transactionProvider,
        IAmACommandProcessor postBox,
        ILogger<GreetingMadeHandlerAsync> logger)
        : RequestHandlerAsync<GreetingMade>
    {
        [UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] 
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICY_ASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default)
        {
            var posts = new List<string>();
            var context = new DynamoDBContext(transactionProvider.DynamoDb);
            var tx = await transactionProvider.GetTransactionAsync(cancellationToken);
            try
            {
                var salutation = new Salutation { Greeting = @event.Greeting };
                var attributes = context.ToDocument(salutation).ToAttributeMap();

                tx.TransactItems.Add(new TransactWriteItem
                {
                    Put = new Put { TableName = "Salutations", Item = attributes }
                });

                posts.Add(await postBox.DepositPostAsync(new SalutationReceived(DateTimeOffset.Now), transactionProvider, cancellationToken: cancellationToken));

                await transactionProvider.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not save salutation");
                await transactionProvider.RollbackAsync(cancellationToken);

                return await base.HandleAsync(@event, cancellationToken);
            }
            finally
            {
                transactionProvider.Close();
            }

            await postBox.ClearOutboxAsync(posts, cancellationToken: cancellationToken);
            
            return await base.HandleAsync(@event,cancellationToken);
        }
    }
}
