using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using DapperExtensions.Predicate;
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
        private readonly IAmATransactionConnectionProvider _transactionConnectionProvider; 

        public FindPersonByNameHandlerAsync(IAmATransactionConnectionProvider transactionConnectionProvider)
        {
            _transactionConnectionProvider = transactionConnectionProvider;
        }
       
        [QueryLogging(0)]
        [RetryableQuery(1, Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = new CancellationToken())
        {
            var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, query.Name);
            var people = await _transactionConnectionProvider.GetConnection().GetListAsync<Person>(searchbyName);
            var person = people.Single();

            return new FindPersonResult(person);
        }
    }
}
