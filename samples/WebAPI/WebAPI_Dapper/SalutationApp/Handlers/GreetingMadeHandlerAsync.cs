using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Inbox.Attributes;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationApp.Entities;
using SalutationApp.Policies;
using SalutationApp.Requests;

namespace SalutationApp.Handlers;

public class GreetingMadeHandlerAsync : RequestHandlerAsync<GreetingMade>
{
    private readonly ILogger<GreetingMadeHandlerAsync> _logger;
    private readonly IAmACommandProcessor _postBox;
    private readonly IAmABoxTransactionProvider<DbTransaction> _transactionConnectionProvider;

    public GreetingMadeHandlerAsync(IAmABoxTransactionProvider<DbTransaction> transactionConnectionProvider,
        IAmACommandProcessor postBox,
        ILogger<GreetingMadeHandlerAsync> logger)
    {
        _transactionConnectionProvider = transactionConnectionProvider;
        _postBox = postBox;
        _logger = logger;
    }

    [UseInboxAsync(0, typeof(GreetingMadeHandlerAsync), true)]
    [RequestLoggingAsync(1, HandlerTiming.Before)]
    [UsePolicyAsync(step: 2, policy: Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
    public override async Task<GreetingMade> HandleAsync(GreetingMade @event,
        CancellationToken cancellationToken = default)
    {
        List<Id> posts = new List<Id>();

        DbTransaction tx = await _transactionConnectionProvider.GetTransactionAsync(cancellationToken);
        DbConnection conn = tx.Connection;
        try
        {
            Salutation salutation = new Salutation(@event.Greeting);

            await conn.ExecuteAsync(
                "insert into Salutation (greeting) values (@greeting)",
                new { greeting = salutation.Greeting },
                tx);

            posts.Add(await _postBox.DepositPostAsync(new SalutationReceived(DateTimeOffset.Now),
                _transactionConnectionProvider, cancellationToken: cancellationToken));

            await _transactionConnectionProvider.CommitAsync(cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not save salutation");

            //if it went wrong rollback entity write and Outbox write
            await _transactionConnectionProvider.RollbackAsync(cancellationToken);

            return await base.HandleAsync(@event, cancellationToken);
        }

        _postBox.ClearOutbox(posts.ToArray());

        return await base.HandleAsync(@event, cancellationToken);
    }
}
