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
    public class AddGreetingHandlerAsync: RequestHandlerAsync<AddGreeting>
    {
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<AddGreetingHandlerAsync> _logger;
        private readonly IAmATransactionConnectionProvider  _transactionProvider;


        public AddGreetingHandlerAsync(IAmATransactionConnectionProvider transactionProvider, IAmACommandProcessor postBox, ILogger<AddGreetingHandlerAsync> logger)
        {
            _transactionProvider = transactionProvider;    //We want to take the dependency on the same instance that will be used via the Outbox, so use the marker interface
            _postBox = postBox;
            _logger = logger;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddGreeting> HandleAsync(AddGreeting addGreeting, CancellationToken cancellationToken = default)
        {
            var posts = new List<Guid>();
            
            //We use the unit of work to grab connection and transaction, because Outbox needs
            //to share them 'behind the scenes'

            var conn = await _transactionProvider.GetConnectionAsync(cancellationToken);
            var tx = await _transactionProvider.GetTransactionAsync(cancellationToken);
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
                    posts.Add(await _postBox.DepositPostAsync(
                        new GreetingMade(greeting.Greet()),
                        _transactionProvider,
                        cancellationToken: cancellationToken));

                    //commit both new greeting and outgoing message
                    await _transactionProvider.CommitAsync(cancellationToken);
                }
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
