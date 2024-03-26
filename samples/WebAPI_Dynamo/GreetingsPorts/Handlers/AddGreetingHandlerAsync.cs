using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class AddGreetingHandlerAsync(
        IAmADynamoDbTransactionProvider transactionProvider,
        IAmACommandProcessor postBox,
        ILogger<AddGreetingHandlerAsync> logger)
        : RequestHandlerAsync<AddGreeting>
    {
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddGreeting> HandleAsync(AddGreeting addGreeting, CancellationToken cancellationToken = default)
        {
            var posts = new List<string>();
            
            //We use the unit of work to grab connection and transaction, because Outbox needs
            //to share them 'behind the scenes'
            var context = new DynamoDBContext(transactionProvider.DynamoDb);
            var transaction = await transactionProvider.GetTransactionAsync(cancellationToken);
            try
            {
                var person = await context.LoadAsync<Person>(addGreeting.Name, cancellationToken);

                person.Greetings.Add(addGreeting.Greeting);

                var document = context.ToDocument(person);
                var attributeValues = document.ToAttributeMap();

                //write the added child entity to the Db - just replace the whole entity as we grabbed the original
                //in production code, an update expression would be faster
                transaction.TransactItems.Add(new TransactWriteItem
                {
                    Put = new Put { TableName = "People", Item = attributeValues }
                });

                //Now write the message we want to send to the Db in the same transaction.
                posts.Add(await postBox.DepositPostAsync(
                    new GreetingMade(addGreeting.Greeting),
                    transactionProvider,
                    cancellationToken: cancellationToken));

                //commit both new greeting and outgoing message
                await transactionProvider.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Exception thrown handling Add Greeting request");
                //it went wrong, rollback the entity change and the downstream message
                await transactionProvider.RollbackAsync(cancellationToken);
                return await base.HandleAsync(addGreeting, cancellationToken);
            }
            finally
            {
                transactionProvider.Close();
            }

            //Send this message via a transport. We need the ids to send just the messages here, not all outstanding ones.
            //Alternatively, you can let the Sweeper do this, but at the cost of increased latency
            await postBox.ClearOutboxAsync(posts, cancellationToken:cancellationToken);

            return await base.HandleAsync(addGreeting, cancellationToken);
        }
    }
}
