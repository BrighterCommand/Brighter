using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Requests;

public class FindGreetingsForPerson : IQuery<FindPersonsGreetings>
{
    public FindGreetingsForPerson(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
