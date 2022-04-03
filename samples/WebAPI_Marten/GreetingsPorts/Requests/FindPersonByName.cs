using GreetingsPorts.Responses;
using Paramore.Darker;

namespace GreetingsPorts.Requests
{
    public class FindPersonByName : IQuery<FindPersonResult>
    {
        public string Name { get; set; }

        public FindPersonByName(string name)
        {
            Name = name;
        }

    }
}
