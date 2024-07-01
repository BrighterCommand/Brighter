using System;
using Paramore.Brighter;

namespace GreetingsPorts.Requests;

public class AddPerson : Command
{
    public AddPerson(string name)
        : base(Guid.NewGuid())
    {
        Name = name;
    }

    public string Name { get; set; }
}
