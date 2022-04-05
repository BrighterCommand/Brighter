using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsEntities;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Handlers
{
    public class FindPersonGreetingsHandlerAsync : QueryHandlerAsync<FindPersonGreetings, FindPersonGreetingsResult>
    {
        private readonly GreetingsEntityGateway unitOfWork;

        public FindPersonGreetingsHandlerAsync(GreetingsEntityGateway unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public override async Task<FindPersonGreetingsResult> ExecuteAsync(FindPersonGreetings query, CancellationToken cancellationToken = default)
        {
            using (unitOfWork)
            {
                var person = await unitOfWork.GetPersonByName(query.PersonName);

                if (person is null)
                {
                    return null;
                }

                person.Greetings ??= new List<Greeting>();

                return new FindPersonGreetingsResult
                {
                    PersonName = person.Name,
                    Greetings = person.Greetings.Select(greeting => new Salutation($"{person.Name}, {greeting.Message}!")),
                };
            }
        }
    }
}
