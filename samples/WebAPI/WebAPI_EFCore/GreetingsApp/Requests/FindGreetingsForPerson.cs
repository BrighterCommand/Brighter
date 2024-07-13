using GreetingsApp.Responses;
using Paramore.Darker;

namespace GreetingsApp.Requests
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
