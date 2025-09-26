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
    public class AddGreetingHandlerAsync(
        GreetingsEntityGateway uow,
        IAmATransactionConnectionProvider provider,
        IAmACommandProcessor postBox)
        : RequestHandlerAsync<AddGreeting>
    {
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<AddGreeting> HandleAsync(AddGreeting addGreeting, CancellationToken cancellationToken = default)
        {
            var posts = new List<Id>();
            
            await provider.GetTransactionAsync(cancellationToken);
            try
            {
                var person = await uow.People
                    .Where(p => p.Name == addGreeting.Name)
                    .SingleAsync(cancellationToken);

                var greeting = new Greeting(addGreeting.Greeting);

                person.AddGreeting(greeting);

                //Now write the message we want to send to the Db in the same transaction.
                posts.Add(await postBox.DepositPostAsync(
                    new GreetingMade(greeting.Greet()),
                    provider,
                    cancellationToken: cancellationToken));

                //write the changed entity to the Db
                await uow.SaveChangesAsync(cancellationToken);

                //write new person and the associated message to the Db
                await provider.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                //it went wrong, rollback the entity change and the downstream message
                await provider.RollbackAsync(cancellationToken);
                return await base.HandleAsync(addGreeting, cancellationToken);
            }
            finally
            {
                provider.Close();
            }

            //Send this message via a transport. We need the ids to send just the messages here, not all outstanding ones.
            //Alternatively, you can let the Sweeper do this, but at the cost of increased latency
            await postBox.ClearOutboxAsync(posts, cancellationToken:cancellationToken);

            return await base.HandleAsync(addGreeting, cancellationToken);
        }
    }
}
