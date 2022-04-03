using System;
using System.Threading;
using System.Threading.Tasks;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
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

        public override Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
