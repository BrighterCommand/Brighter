using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Handlers
{
    public class FindPersonByNameHandlerAsync : QueryHandlerAsync<FindPersonByName, FindPersonResult>
    {
        public FindPersonByNameHandlerAsync()
        {
        }
        
        public override async Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = new CancellationToken())
        {
            /*
            var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, query.Name);
            var people = await _uow.Database.GetListAsync<Person>(searchbyName);
            var person = people.Single();
            */

            return new FindPersonResult(null /*person*/);
        }
    }
}
