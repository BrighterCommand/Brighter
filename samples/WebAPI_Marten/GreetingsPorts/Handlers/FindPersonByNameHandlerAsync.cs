using GreetingsPorts.EntityGateway.Interfaces;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Handlers
{
    public class FindPersonByNameHandlerAsync : QueryHandlerAsync<FindPersonByName, FindPersonResult>
    {
        private readonly IGreetingsEntityGateway unitOfWork;

        public FindPersonByNameHandlerAsync(IGreetingsEntityGateway unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public override async Task<FindPersonResult> ExecuteAsync(FindPersonByName query, CancellationToken cancellationToken = default)
        {
            using (unitOfWork)
            {
                var person = await unitOfWork.GetPersonByName(query.Name);

                if (person is null)
                {
                    return null;
                }

                return new FindPersonResult(person);
            }
        }
    }
}
