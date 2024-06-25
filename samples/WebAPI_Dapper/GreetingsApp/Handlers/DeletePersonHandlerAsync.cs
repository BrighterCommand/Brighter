using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GreetingsPorts.Entities;
using GreetingsPorts.Requests;
using Microsoft.Extensions.Logging;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class DeletePersonHandlerAsync : RequestHandlerAsync<DeletePerson>
    {
        private readonly IAmARelationalDbConnectionProvider _relationalDbConnectionProvider;
        private readonly ILogger<DeletePersonHandlerAsync> _logger;

        public DeletePersonHandlerAsync(IAmARelationalDbConnectionProvider relationalDbConnectionProvider,
            ILogger<DeletePersonHandlerAsync> logger)
        {
            _relationalDbConnectionProvider = relationalDbConnectionProvider;
            _logger = logger;
        }

        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default)
        {
            var connection = await _relationalDbConnectionProvider.GetConnectionAsync(cancellationToken);
            var tx = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var people = await connection.QueryAsync<Person>(
                    "select * from Person where name = @name",
                    new {name = deletePerson.Name},
                    tx
                    );
                var person = people.SingleOrDefault();

                if (person != null)
                {
                    await connection.ExecuteAsync(
                        "delete from Greeting where Recipient_Id = @PersonId",
                        new { PersonId = person.Id },
                        tx);
                    
                    await connection.ExecuteAsync("delete from Person where Id = @Id",
                        new {Id = person.Id},
                        tx);

                    await tx.CommitAsync(cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown handling Add Greeting request");
                //it went wrong, rollback the entity change and the downstream message
                await tx.RollbackAsync(cancellationToken);
                return await base.HandleAsync(deletePerson, cancellationToken);
            }
            finally
            {
                await connection.DisposeAsync();
                await tx.DisposeAsync();

            }

            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
