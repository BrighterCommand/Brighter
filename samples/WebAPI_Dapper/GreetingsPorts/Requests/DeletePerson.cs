using System;
using Paramore.Brighter;

namespace GreetingsPorts.Requests
{
    public class DeletePerson : Command
    {
        public string Name { get; }

        public DeletePerson(string name)
            : base(Guid.NewGuid())
        {
            Name = name;
        }
    }
}
