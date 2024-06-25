using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GreetingsPorts.Entities;
using GreetingsPorts.Policies;
using GreetingsPorts.Requests;
using GreetingsPorts.Responses;
using Paramore.Brighter;
using Paramore.Darker;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsPorts.Handlers
{
    public class FIndGreetingsForPersonHandlerAsync : QueryHandlerAsync<FindGreetingsForPerson, FindPersonsGreetings>
    {
        private readonly IAmARelationalDbConnectionProvider _relationalDbConnectionProvider;

        public FIndGreetingsForPersonHandlerAsync(IAmARelationalDbConnectionProvider relationalDbConnectionProvider)
        {
            _relationalDbConnectionProvider = relationalDbConnectionProvider;
        }

        [QueryLogging(0)]
        [RetryableQuery(1, Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
        public override async Task<FindPersonsGreetings> ExecuteAsync(FindGreetingsForPerson query, CancellationToken cancellationToken = new CancellationToken())
        {
            //Retrieving parent and child is a bit tricky with Dapper. From raw SQL We wget back a set that has a row-per-child. We need to turn that
            //into one entity per parent, with a collection of children. To do that we bring everything back into memory, group by parent id and collate all
            //the children for that group.

            var sql = @"select p.Id, p.Name, g.Id, g.Message 
                        from Person p
                        inner join Greeting g on g.Recipient_Id = p.Id
                         where p.Name = @name";
            await using var connection = await _relationalDbConnectionProvider.GetConnectionAsync(cancellationToken);
            var people = await connection.QueryAsync<Person, Greeting, Person>(sql, (person, greeting) =>
            {
                person.Greetings.Add(greeting);
                return person;
            }, 
            param: new {name = query.Name},
            splitOn: "Id");
            
            if (!people.Any())
            {
                return new FindPersonsGreetings(){Name = query.Name, Greetings = Array.Empty<Salutation>()};
            }

            var peopleGreetings = people.GroupBy(p => p.Id).Select(grp =>
            {
                var groupedPerson = grp.First();
                groupedPerson.Greetings = grp.Select(p => p.Greetings.Single()).ToList();
                return groupedPerson;
            });

            var person = peopleGreetings.First();

            return new FindPersonsGreetings
            {
                Name = person.Name, Greetings = person.Greetings.Select(g => new Salutation(g.Greet()))
            };
        }
        
    }
}
