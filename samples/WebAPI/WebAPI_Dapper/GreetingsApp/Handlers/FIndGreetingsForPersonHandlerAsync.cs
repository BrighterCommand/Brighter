using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using GreetingsApp.Entities;
using GreetingsApp.Policies;
using GreetingsApp.Requests;
using GreetingsApp.Responses;
using Paramore.Brighter;
using Paramore.Darker;
using Paramore.Darker.Policies;
using Paramore.Darker.QueryLogging;

namespace GreetingsApp.Handlers;

public class FIndGreetingsForPersonHandlerAsync : QueryHandlerAsync<FindGreetingsForPerson, FindPersonsGreetings>
{
    private readonly IAmARelationalDbConnectionProvider _relationalDbConnectionProvider;

    public FIndGreetingsForPersonHandlerAsync(IAmARelationalDbConnectionProvider relationalDbConnectionProvider)
    {
        _relationalDbConnectionProvider = relationalDbConnectionProvider;
    }

    [QueryLogging(0)]
    [RetryableQuery(1, Retry.EXPONENTIAL_RETRYPOLICYASYNC)]
    public override async Task<FindPersonsGreetings> ExecuteAsync(FindGreetingsForPerson query,
        CancellationToken cancellationToken = new())
    {
        //Retrieving parent and child is a bit tricky with Dapper. From raw SQL We wget back a set that has a row-per-child. We need to turn that
        //into one entity per parent, with a collection of children. To do that we bring everything back into memory, group by parent id and collate all
        //the children for that group.

        string sql = @"select p.Id, p.Name, g.Id, g.Message 
                        from Person p
                        inner join Greeting g on g.Recipient_Id = p.Id
                         where p.Name = @name";
        await using DbConnection connection =
            await _relationalDbConnectionProvider.GetConnectionAsync(cancellationToken);
        IEnumerable<Person> people = await connection.QueryAsync<Person, Greeting, Person>(sql, (person, greeting) =>
            {
                person.Greetings.Add(greeting);
                return person;
            },
            new { name = query.Name },
            splitOn: "Id");

        if (!people.Any()) return new FindPersonsGreetings { Name = String.Empty, Greetings = Array.Empty<Salutation>() };

        IEnumerable<Person> peopleGreetings = people.GroupBy(p => p.Id).Select(grp =>
        {
            Person groupedPerson = grp.First();
            groupedPerson.Greetings = grp.Select(p => p.Greetings.Single()).ToList();
            return groupedPerson;
        });

        Person person = peopleGreetings.First();

        return new FindPersonsGreetings
        {
            Name = person.Name, Greetings = person.Greetings.Select(g => new Salutation(g.Greet()))
        };
    }
}
