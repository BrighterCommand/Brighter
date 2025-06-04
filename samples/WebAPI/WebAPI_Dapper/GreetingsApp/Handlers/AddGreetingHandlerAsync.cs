using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GreetingsApp.Entities;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsApp.Handlers;

public class AddGreetingHandlerAsync : RequestHandlerAsync<AddGreeting>
{
    private readonly ILogger<AddGreetingHandlerAsync> _logger;
    private readonly IAmACommandProcessor _postBox;
    private readonly IAmATransactionConnectionProvider _transactionProvider;

    public AddGreetingHandlerAsync(IAmATransactionConnectionProvider transactionProvider,
        IAmACommandProcessor postBox,
        ILogger<AddGreetingHandlerAsync> logger)
    {
        _transactionProvider = transactionProvider;
        _postBox = postBox;
        _logger = logger;
    }

    [RequestLoggingAsync(0, HandlerTiming.Before)]
    [UsePolicyAsync(step: 1, policy: Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
    public override async Task<AddGreeting> HandleAsync(
        AddGreeting addGreeting,
        CancellationToken cancellationToken = default
        )
    {
        List<Id> posts = new List<Id>();

        //We use the unit of work to grab connection and transaction, because Outbox needs
        //to share them 'behind the scenes'

        DbConnection conn = await _transactionProvider.GetConnectionAsync(cancellationToken);
        DbTransaction tx = await _transactionProvider.GetTransactionAsync(cancellationToken);
        try
        {
            IEnumerable<Person> people = await conn.QueryAsync<Person>(
                "select * from Person where name = @name",
                new { name = addGreeting.Name },
                tx
            );
            Person person = people.SingleOrDefault();

            if (person != null)
            {
                Greeting greeting = new Greeting(addGreeting.Greeting, person);

                //write the added child entity to the Db
                await conn.ExecuteAsync(
                    "insert into Greeting (Message, Recipient_Id) values (@Message, @RecipientId)",
                    new { greeting.Message, greeting.RecipientId },
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

        //NOTE: This immediately send this message via the transport. We need the ids to send just the messages here,
        //not all outstanding ones. Alternatively, you can let the Sweeper do this,
        //but at the cost of increased latency. To see that, comment out the line below and run Greetings_Sweeper
        await _postBox.ClearOutboxAsync(posts, cancellationToken: cancellationToken);

        return await base.HandleAsync(addGreeting, cancellationToken);
    }
}
