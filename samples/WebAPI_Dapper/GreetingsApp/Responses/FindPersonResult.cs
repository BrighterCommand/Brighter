using GreetingsApp.Entities;

namespace GreetingsApp.Responses;

public class FindPersonResult
{
    public FindPersonResult(Person person)
    {
        Person = person;
    }

    public Person Person { get; private set; }
}
