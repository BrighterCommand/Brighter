using GreetingsApp.Responses;
using Paramore.Darker;

namespace GreetingsApp.Requests;

public class FindPersonByName : IQuery<FindPersonResult>
{
    public FindPersonByName(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
