using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DapperExtensions;
using DapperExtensions.Predicate;
using GreetingsEntities;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Brighter.Dapper;
using Paramore.Darker;

namespace GreetingsPorts.Handlers
{
    public class FIndGreetingsForPersonHandlerAsync : QueryHandlerAsync<FindGreetingsForPerson, FindPersonsGreetings>
    {
        private readonly IUnitOfWork _uow;

        public FIndGreetingsForPersonHandlerAsync(IUnitOfWork uow)
        {
            _uow = uow;
        }
        
        public override async Task<FindPersonsGreetings> ExecuteAsync(FindGreetingsForPerson query, CancellationToken cancellationToken = new CancellationToken())
        {
            
            var searchbyName = Predicates.Field<Person>(p => p.Name, Operator.Eq, query.Name);
            var people = await _uow.Database.GetListAsync<Person>(searchbyName);
            var person = people.Single();
            

            return new FindPersonsGreetings
            {
                Name = person.Name, 
                Greetings = person.Greetings.Select(g => new Salutation(g.Greet()))
            };

        }
        
    }
}
