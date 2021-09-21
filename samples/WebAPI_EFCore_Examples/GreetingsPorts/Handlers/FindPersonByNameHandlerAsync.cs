using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Microsoft.EntityFrameworkCore;
using Paramore.Darker;

namespace GreetingsPorts.Handlers
{
    public class FindPersonByNameHandlerAsync : QueryHandlerAsync<FindPersonByName, FindPersonResult>
    {
        private readonly GreetingsEntityGateway _uow;

        public FindPersonByNameHandlerAsync(GreetingsEntityGateway uow)
        {
            _uow = uow;
        }
        
        public override async Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = new CancellationToken())
        {
            return await _uow.People
                .Where(p => p.Name == query.Name)
                .Select(p => new FindPersonResult(p))
                .SingleAsync(cancellationToken);
        }
    }
}
