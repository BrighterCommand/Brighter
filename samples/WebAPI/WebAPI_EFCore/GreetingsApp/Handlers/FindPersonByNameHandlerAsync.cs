using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsApp.EntityGateway;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using GreetingsApp.Responses;
using Microsoft.EntityFrameworkCore;
using Paramore.Darker;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsApp.Handlers
{
    public class FindPersonByNameHandlerAsync : QueryHandlerAsync<FindPersonByName, FindPersonResult?>
    {
        private readonly GreetingsEntityGateway _uow;

        public FindPersonByNameHandlerAsync(GreetingsEntityGateway uow)
        {
            _uow = uow;
        }

        [QueryLogging(0)]
        [RetryableQuery(1, Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<FindPersonResult?> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = new CancellationToken())
        {
            return await _uow.People
                .Where(p => p.Name == query.Name)
                .Select(p => new FindPersonResult(p))
                .SingleOrDefaultAsync(cancellationToken);
        }
    }
}
