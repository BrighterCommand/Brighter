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
        private readonly IAmATransactionConnectionProvider _transactionConnectionProvider;

        public DeletePersonHandlerAsync(IAmATransactionConnectionProvider transactionConnectionProvider)
        {
            _transactionConnectionProvider = transactionConnectionProvider;
        }
        
        [RequestLoggingAsync(0, HandlerTiming.Before)]
        [UsePolicyAsync(step:1, policy: Policies.Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public async override Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default)
        {
            using (var connection = await _transactionConnectionProvider.GetConnectionAsync(cancellationToken))
            {
                await connection.OpenAsync(cancellationToken);
                var tx = await _transactionConnectionProvider.GetTransactionAsync(cancellationToken);
                try
                {

                    var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, deletePerson.Name);
                    var people = await _transactionConnectionProvider.GetConnection()
                        .GetListAsync<Person>(searchbyName, transaction: tx);
                    var person = people.Single();

                    var deleteById = Predicates.Field<Greeting>(g => g.RecipientId, Operator.Eq, person.Id);
                    await _transactionConnectionProvider.GetConnection().DeleteAsync(deleteById, tx);

                    await tx.CommitAsync(cancellationToken);
                }
                catch (Exception)
                {
                    //it went wrong, rollback the entity change and the downstream message
                    await tx.RollbackAsync(cancellationToken);
                    return await base.HandleAsync(deletePerson, cancellationToken);
                }
            }

            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
