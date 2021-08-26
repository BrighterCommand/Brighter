using System;
using GreetingsInteractors.Responses;
using Paramore.Darker;

namespace GreetingsInteractors.Requests
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
