using GreetingsApp.Responses;
using Paramore.Darker;

namespace GreetingsApp.Requests
{
    public class FindPersonByName : IQuery<FindPersonResult>
    {
        public string Name { get; }

        public FindPersonByName(string name)
        {
            Name = name;
        }
    }
}
