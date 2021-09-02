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
    public class FIndGreetingsForPersonHandlerAsync : QueryHandlerAsync<FindGreetingsForPerson, FindPersonsGreetings>
    {
        private readonly GreetingsEntityGateway _uow;

        public FIndGreetingsForPersonHandlerAsync(GreetingsEntityGateway uow)
        {
            _uow = uow;
        }
        
        public override async Task<FindPersonsGreetings> ExecuteAsync(FindGreetingsForPerson query, CancellationToken cancellationToken = new CancellationToken())
        {
            var person = await _uow.People
                .Where(p => p.Name == query.Name)
                .SingleAsync(cancellationToken);

            if (person == null) return null;

            return new FindPersonsGreetings { Greetings = person.Greetings.Select(g => new Salutation(g.Greet())) };

        }
        
    }
}
