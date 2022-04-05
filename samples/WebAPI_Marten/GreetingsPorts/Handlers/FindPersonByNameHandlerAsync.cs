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
        private readonly GreetingsEntityGateway unitOfWork;

        public FindPersonByNameHandlerAsync(GreetingsEntityGateway unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public override async Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = default)
        {
            using (unitOfWork)
            {
                var person = await unitOfWork.GetByName(query.Name);
                return new FindPersonResult(person);
            }
        }
    }
}
