using System;
using Paramore.Brighter;

namespace GreetingsApp.Requests
{
    public class DeletePerson(string name) : Command(Id.Random())
    {
        public string Name { get; } = name;
    }
}
