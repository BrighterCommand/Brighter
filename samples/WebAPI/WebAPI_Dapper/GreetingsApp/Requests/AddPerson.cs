using System;
using Paramore.Brighter;

namespace GreetingsApp.Requests;

public class AddPerson(string name) : Command(Id.Random())
{
    public string Name { get; set; } = name;
}
