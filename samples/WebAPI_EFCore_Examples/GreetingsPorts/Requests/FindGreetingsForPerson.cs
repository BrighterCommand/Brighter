using GreetingsInteractors.Responses;
using Paramore.Darker;

namespace GreetingsInteractors.Requests
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
