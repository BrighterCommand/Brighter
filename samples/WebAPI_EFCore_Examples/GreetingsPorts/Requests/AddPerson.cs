using System;
using Paramore.Brighter;

namespace GreetingsInteractors.Requests
{
    public class AddPerson : Command
    {
        public string Name { get; set; }

        public AddPerson(string name) 
            : base(Guid.NewGuid())
        {
            Name = name;
        }
    }
}
