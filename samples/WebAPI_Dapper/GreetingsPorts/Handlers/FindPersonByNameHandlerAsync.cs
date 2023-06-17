using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GreetingsEntities;
using GreetingsPorts.Policies;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Brighter;
using Paramore.Darker;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsPorts.Handlers
{
    public class FindPersonByNameHandlerAsync : QueryHandlerAsync<FindPersonByName, FindPersonResult>
    {
        private readonly IAmARelationalDbConnectionProvider  _relationalDbConnectionProvider; 

        public FindPersonByNameHandlerAsync(IAmARelationalDbConnectionProvider relationalDbConnectionProvider)
        {
            _relationalDbConnectionProvider = relationalDbConnectionProvider;
        }
       
        [QueryLogging(0)]
        [RetryableQuery(1, Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = new CancellationToken())
        {
            await using var connection = await _relationalDbConnectionProvider .GetConnectionAsync(cancellationToken);
            var people = await connection.QueryAsync<Person>("select * from Person where name = @name", new {name = query.Name});
            var person = people.SingleOrDefault();

            return new FindPersonResult(person);
        }
    }
}
