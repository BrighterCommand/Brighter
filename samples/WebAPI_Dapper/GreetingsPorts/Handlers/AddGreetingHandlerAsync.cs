using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Paramore.Brighter;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class AddGreetingHandlerAsync(
        IAmATransactionConnectionProvider transactionProvider,
        IAmACommandProcessor postBox,
        ILogger<AddGreetingHandlerAsync> logger)
        : RequestHandlerAsync<AddGreeting>
    {
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddGreeting> HandleAsync(AddGreeting addGreeting, CancellationToken cancellationToken = default)
        {
            var posts = new List<Guid>();
            
            //We use the unit of work to grab connection and transaction, because Outbox needs
            //to share them 'behind the scenes'

            var conn = await transactionProvider.GetConnectionAsync(cancellationToken);
            var tx = await transactionProvider.GetTransactionAsync(cancellationToken);
            try
            {
                var people = await conn.QueryAsync<Person>(
                    "select * from Person where name = @name",
                    new {name = addGreeting.Name},
                    tx
                );
                var person = people.SingleOrDefault();

                if (person != null)
                {
                    var greeting = new Greeting(addGreeting.Greeting, person);

                    //write the added child entity to the Db
                    await conn.ExecuteAsync(
                        "insert into Greeting (Message, Recipient_Id) values (@Message, @RecipientId)",
                        new { greeting.Message, RecipientId = greeting.RecipientId },
                        tx);

                    //Now write the message we want to send to the Db in the same transaction.
                    posts.Add(await postBox.DepositPostAsync(
                        new GreetingMade(greeting.Greet()),
                        transactionProvider,
                        cancellationToken: cancellationToken));

                    //commit both new greeting and outgoing message
                    await transactionProvider.CommitAsync(cancellationToken);
                }
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
