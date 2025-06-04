using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.Model;
using Paramore.Brighter;
using GreetingsEntities;
using GreetingsApp.Requests;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.DynamoDb;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsApp.Handlers
{
    public class AddGreetingHandlerAsync : RequestHandlerAsync<AddGreeting>
    {
        private readonly IAmADynamoDbTransactionProvider _transactionProvider;
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<AddGreetingHandlerAsync> _logger;

        public AddGreetingHandlerAsync(IAmADynamoDbTransactionProvider transactionProvider,
            IAmACommandProcessor postBox,
            ILogger<AddGreetingHandlerAsync> logger)
        {
            _transactionProvider = transactionProvider;
            _postBox = postBox;
            _logger = logger;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddGreeting> HandleAsync(AddGreeting addGreeting, CancellationToken cancellationToken = default)
        {
            var posts = new List<Id>();
            
            //We use the unit of work to grab connection and transaction, because Outbox needs
            //to share them 'behind the scenes'
            var context = new DynamoDBContext(_transactionProvider.DynamoDb);
            var transaction = await _transactionProvider.GetTransactionAsync(cancellationToken);
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
                posts.Add(await _postBox.DepositPostAsync(
                    new GreetingMade(addGreeting.Greeting),
                    _transactionProvider,
                    cancellationToken: cancellationToken));

                //commit both new greeting and outgoing message
                await _transactionProvider.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown handling Add Greeting request");
                //it went wrong, rollback the entity change and the downstream message
                await _transactionProvider.RollbackAsync(cancellationToken);
                return await base.HandleAsync(addGreeting, cancellationToken);
            }
            finally
            {
                _transactionProvider.Close();
            }

            //Send this message via a transport. We need the ids to send just the messages here, not all outstanding ones.
            //Alternatively, you can let the Sweeper do this, but at the cost of increased latency
            await _postBox.ClearOutboxAsync(posts, cancellationToken:cancellationToken);

            return await base.HandleAsync(addGreeting, cancellationToken);
        }
    }
}
