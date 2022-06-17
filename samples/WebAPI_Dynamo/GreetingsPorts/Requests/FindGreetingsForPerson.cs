using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Requests
{
    public class FindGreetingsForPerson : IQuery<FindPersonsGreetings>
    {
        public string Name { get; }

        public FindGreetingsForPerson(string name)
        {
            Name = name;
        }
    }
}
