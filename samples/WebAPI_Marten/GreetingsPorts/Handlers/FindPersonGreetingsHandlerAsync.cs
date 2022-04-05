using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GreetingsPorts.EntityGateway;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Handlers
{
    public class FindPersonGreetingsHandlerAsync : QueryHandlerAsync<FindPersonGreetings, FindPersonGreetingsResult>
    {
        private readonly GreetingsEntityGateway _uow;

        public FindPersonGreetingsHandlerAsync(GreetingsEntityGateway ouw)
        {
            _uow = ouw;
        }

        public override async Task<FindPersonGreetingsResult> ExecuteAsync(FindPersonGreetings query, CancellationToken cancellationToken = default)
        {
            var person = await _uow.GetByName(query.PersonName);

            if (person is null)
            {
                return null;
            }

            return new FindPersonGreetingsResult
            {
                PersonName = person.Name,
                Greetings = person.Greetings.Select(greeting => new Salutation($"{person.Name}, {greeting.Message}!")),
            };
        }
    }
}
