using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Requests
{
    public class FindPersonGreetings : IQuery<FindPersonGreetingsResult>
    {
        public string PersonName { get; }

        public FindPersonGreetings(string personName)
        {
            PersonName = personName;
        }

    }
}
