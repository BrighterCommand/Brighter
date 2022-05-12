using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using DapperExtensions.Predicate;
using GreetingsEntities;
using GreetingsPorts.Requests;
using Paramore.Brighter;
using Paramore.Brighter.Dapper;

namespace GreetingsPorts.Handlers
{
    public class DeletePersonHandlerAsync : RequestHandlerAsync<DeletePerson>
    {
        private readonly IUnitOfWork _uow;

        public DeletePersonHandlerAsync(IUnitOfWork uow)
        {
            _uow = uow;
        }
        public async override Task<DeletePerson> HandleAsync(DeletePerson deletePerson, CancellationToken cancellationToken = default(CancellationToken))
        {
            var tx = await _uow.BeginOrGetTransactionAsync(cancellationToken);
            try
            {

                var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, deletePerson.Name);
                var people = await _uow.Database.GetListAsync<Person>(searchbyName, transaction: tx);
                var person = people.Single();

                var deleteById = Predicates.Field<Greeting>(g => g.RecipientId, Operator.Eq, person.Id);
                await _uow.Database.DeleteAsync(deleteById, tx);
                
                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                //it went wrong, rollback the entity change and the downstream message
                await tx.RollbackAsync(cancellationToken);
                return await base.HandleAsync(deletePerson, cancellationToken);
            }

            return await base.HandleAsync(deletePerson, cancellationToken);
        }
    }
}
