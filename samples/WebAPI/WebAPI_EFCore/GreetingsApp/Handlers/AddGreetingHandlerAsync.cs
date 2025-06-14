using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsApp.Entities;
using GreetingsApp.EntityGateway;
using GreetingsApp.Requests;
using Microsoft.EntityFrameworkCore;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsApp.Handlers
{
    public class AddGreetingHandlerAsync : RequestHandlerAsync<AddGreeting>
    {
        private readonly GreetingsEntityGateway _uow;
        private readonly IAmATransactionConnectionProvider _provider;
        private readonly IAmACommandProcessor _postBox;

        public AddGreetingHandlerAsync(GreetingsEntityGateway uow,
            IAmATransactionConnectionProvider provider,
            IAmACommandProcessor postBox)
        {
            _uow = uow;
            _provider = provider;
            _postBox = postBox;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddGreeting> HandleAsync(AddGreeting addGreeting, CancellationToken cancellationToken = default)
        {
            var posts = new List<Id>();
            
            await _provider.GetTransactionAsync(cancellationToken);
            try
            {
                var person = await _uow.People
                    .Where(p => p.Name == addGreeting.Name)
                    .SingleAsync(cancellationToken);

                var greeting = new Greeting(addGreeting.Greeting);

                person.AddGreeting(greeting);

                //Now write the message we want to send to the Db in the same transaction.
                posts.Add(await _postBox.DepositPostAsync(
                    new GreetingMade(greeting.Greet()),
                    _provider,
                    cancellationToken: cancellationToken));

                //write the changed entity to the Db
                await _uow.SaveChangesAsync(cancellationToken);

                //write new person and the associated message to the Db
                await _provider.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                //it went wrong, rollback the entity change and the downstream message
                await _provider.RollbackAsync(cancellationToken);
                return await base.HandleAsync(addGreeting, cancellationToken);
            }
            finally
            {
                _provider.Close();
            }

            //Send this message via a transport. We need the ids to send just the messages here, not all outstanding ones.
            //Alternatively, you can let the Sweeper do this, but at the cost of increased latency
            await _postBox.ClearOutboxAsync(posts, cancellationToken:cancellationToken);

            return await base.HandleAsync(addGreeting, cancellationToken);
        }
    }
}
