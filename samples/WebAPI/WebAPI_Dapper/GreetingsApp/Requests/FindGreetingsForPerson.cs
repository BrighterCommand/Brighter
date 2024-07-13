using GreetingsApp.Responses;
using Paramore.Darker;

namespace GreetingsApp.Requests;

public class FindGreetingsForPerson : IQuery<FindPersonsGreetings>
{
    public FindGreetingsForPerson(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
