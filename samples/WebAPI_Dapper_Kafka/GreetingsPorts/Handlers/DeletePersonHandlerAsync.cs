using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using DapperExtensions.Predicate;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.Logging.Attributes;
using Paramore.Brighter.Policies.Attributes;

namespace GreetingsPorts.Handlers
{
    public class DeletePersonHandlerAsync : RequestHandlerAsync<DeletePerson>
    {
        private readonly IAmARelationalDbConnectionProvider _relationalDbConnectionProvider;

        public DeletePersonHandlerAsync(IAmARelationalDbConnectionProvider relationalDbConnectionProvider)
        {
            _relationalDbConnectionProvider = relationalDbConnectionProvider;
        }
        
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default)
        {
            var connection = await _relationalDbConnectionProvider.GetConnectionAsync(cancellationToken);
            await connection.OpenAsync(cancellationToken);
            
            //NOTE: we are using a transaction, but a connection provider will not manage one for us, so we need to do it ourselves
            var tx = await connection.BeginTransactionAsync(cancellationToken);
            try
            {

                var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, deletePerson.Name);
                var people = await _relationalDbConnectionProvider.GetConnection()
                    .GetListAsync<Person>(searchbyName, transaction: tx);
                var person = people.Single();

                var deleteById = Predicates.Field<Greeting>(g => g.RecipientId, Operator.Eq, person.Id);
                await _relationalDbConnectionProvider.GetConnection().DeleteAsync(deleteById, tx);

                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
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
