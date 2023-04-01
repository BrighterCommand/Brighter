using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Dapper;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;
using SalutationEntities;
using SalutationPorts.Requests;

namespace SalutationPorts.Handlers
{
    public class GreetingMadeHandlerAsync : RequestHandlerAsync<GreetingMade>
    {
        private readonly IUnitOfWork _uow;
        private readonly IAmACommandProcessor _postBox;
        private readonly ILogger<GreetingMadeHandlerAsync> _logger;

        public GreetingMadeHandlerAsync(IUnitOfWork uow, IAmACommandProcessor postBox, ILogger<GreetingMadeHandlerAsync> logger)
        {
            _uow = uow;
            _postBox = postBox;
            _logger = logger;
        }

        //[UseInboxAsync(step:0, contextKey: typeof(GreetingMadeHandlerAsync), onceOnly: true )] -- we are using a global inbox, so need to be explicit!!
        [RequestLoggingAsync(step: 1, timing: HandlerTiming.Before)]
        [UsePolicyAsync(step:2, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<GreetingMade> HandleAsync(GreetingMade @event, CancellationToken cancellationToken = default(CancellationToken))
        {
            var posts = new List<Guid>();

            var tx = await _uow.BeginOrGetTransactionAsync(cancellationToken);
            try
            {
                var salutation = new Salutation(@event.Greeting);

                await _uow.Database.InsertAsync<Salutation>(salutation, tx);

                posts.Add(await _postBox.DepositPostAsync(new SalutationReceived(DateTimeOffset.Now), cancellationToken: cancellationToken));

                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not save salutation");

                //if it went wrong rollback entity write and Outbox write
                await tx.RollbackAsync(cancellationToken);

                return await base.HandleAsync(@event, cancellationToken);
            }

            await _postBox.ClearOutboxAsync(posts, cancellationToken: cancellationToken);

            return await base.HandleAsync(@event, cancellationToken);
        }
    }
}
