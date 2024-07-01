using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Requests;

public class FindPersonByName : IQuery<FindPersonResult>
{
    public FindPersonByName(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
