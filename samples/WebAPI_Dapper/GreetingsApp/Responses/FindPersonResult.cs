using GreetingsPorts.Entities;

namespace GreetingsPorts.Responses;

public class FindPersonResult
{
    public FindPersonResult(Person person)
    {
        Person = person;
    }

    public Person Person { get; private set; }
}
