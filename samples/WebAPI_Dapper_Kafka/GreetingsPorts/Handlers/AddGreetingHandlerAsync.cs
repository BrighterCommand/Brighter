using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using DapperExtensions.Predicate;
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
        private readonly IAmATransactionConnectionProvider  _transactionConnectionProvider;


        public AddGreetingHandlerAsync(IAmATransactionConnectionProvider transactionConnectionProvider, IAmACommandProcessor postBox, ILogger<AddGreetingHandlerAsync> logger)
        {
            _transactionConnectionProvider = transactionConnectionProvider;    //We want to take the dependency on the same instance that will be used via the Outbox, so use the marker interface
            _postBox = postBox;
            _logger = logger;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddGreeting> HandleAsync(AddGreeting addGreeting, CancellationToken cancellationToken = default)
        {
            var posts = new List<Guid>();

            using (var conn = await _transactionConnectionProvider.GetConnectionAsync(cancellationToken))
            {
                await conn.OpenAsync(cancellationToken);
                using (var tx = _transactionConnectionProvider.GetTransaction())
                {
                    try
                    {
                        var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, addGreeting.Name);
                        var people = await conn.GetListAsync<Person>(searchbyName, transaction: tx);
                        var person = people.Single();

                        var greeting = new Greeting(addGreeting.Greeting, person);

                        //write the added child entity to the Db
                        await conn.InsertAsync<Greeting>(greeting, tx);

                        //Now write the message we want to send to the Db in the same transaction.
                        posts.Add(await _postBox.DepositPostAsync(new GreetingMade(greeting.Greet()),
                            cancellationToken: cancellationToken));

                        //commit both new greeting and outgoing message
                        await tx.CommitAsync(cancellationToken);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Exception thrown handling Add Greeting request");
                        //it went wrong, rollback the entity change and the downstream message
                        await tx.RollbackAsync(cancellationToken);
                        return await base.HandleAsync(addGreeting, cancellationToken);
                    }
                }
            }

            //Send this message via a transport. We need the ids to send just the messages here, not all outstanding ones.
            //Alternatively, you can let the Sweeper do this, but at the cost of increased latency
            await _postBox.ClearOutboxAsync(posts, cancellationToken:cancellationToken);

            return await base.HandleAsync(addGreeting, cancellationToken);
        }
    }
}
