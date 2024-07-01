using System;
using Paramore.Brighter;

namespace GreetingsPorts.Requests;

public class DeletePerson : Command
{
    public DeletePerson(string name)
        : base(Guid.NewGuid())
    {
        Name = name;
    }

    public string Name { get; }
}
