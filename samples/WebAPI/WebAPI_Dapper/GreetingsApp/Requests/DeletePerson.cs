using System;
using Paramore.Brighter;

namespace GreetingsApp.Requests;

public class DeletePerson : Command
{
    public DeletePerson(string name)
        : base(Id.Random)
    {
        Name = name;
    }

    public string Name { get; }
}
