using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using DapperExtensions.Predicate;
using GreetingsEntities;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Brighter.Dapper;
using Paramore.Darker;
using Paramore.Darker.QueryLogging;

namespace GreetingsPorts.Handlers
{
    public class FindPersonByNameHandlerAsync : QueryHandlerAsync<FindPersonByName, FindPersonResult>
    {
        private readonly IUnitOfWork _uow;

        public FindPersonByNameHandlerAsync(IUnitOfWork uow)
        {
            _uow = uow;
        }
       
        [QueryLogging(0)]
        public override async Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = new CancellationToken())
        {
            var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, query.Name);
            var people = await _uow.Database.GetListAsync<Person>(searchbyName);
            var person = people.Single();

            return new FindPersonResult(person);
        }
    }
}
