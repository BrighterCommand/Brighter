using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Microsoft.Extensions.Logging;

namespace GreetingsPorts.Handlers
{
    public class AddGreetingHandlerAsync: RequestHandlerAsync<AddGreeting>
    {
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<AddGreetingHandlerAsync> _logger;


        public AddGreetingHandlerAsync(IAmACommandProcessor postBox, ILogger<AddGreetingHandlerAsync> logger)
        {
            _postBox = postBox;
            _logger = logger;
        }

        public override async Task<AddGreeting> HandleAsync(AddGreeting addGreeting, CancellationToken cancellationToken = default(CancellationToken))
        {
            var posts = new List<Guid>();
            
            //We use the unit of work to grab connection and transaction, because Outbox needs
            //to share them 'behind the scenes'
           /* 
            var tx = await _uow.BeginOrGetTransactionAsync(cancellationToken);
            try
            {
                var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, addGreeting.Name);
                var people = await _uow.Database.GetListAsync<Person>(searchbyName, transaction: tx);
                var person = people.Single();
                
                var greeting = new Greeting(addGreeting.Greeting, person);
                
               //write the added child entity to the Db
                await _uow.Database.InsertAsync<Greeting>(greeting, tx);

                //Now write the message we want to send to the Db in the same transaction.
                posts.Add(await _postBox.DepositPostAsync(new GreetingMade(greeting.Greet()), cancellationToken: cancellationToken));
                
                //commit both new greeting and outgoing message
                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {   
                _logger.LogError(e, "Exception thrown handling Add Greeting request");
                //it went wrong, rollback the entity change and the downstream message
                //await tx.RollbackAsync(cancellationToken);
                return await base.HandleAsync(addGreeting, cancellationToken);
            }

            //Send this message via a transport. We need the ids to send just the messages here, not all outstanding ones.
            //Alternatively, you can let the Sweeper do this, but at the cost of increased latency
            await _postBox.ClearOutboxAsync(posts, cancellationToken:cancellationToken);
            
            */

            return await base.HandleAsync(addGreeting, cancellationToken);
        }
    }
}
