using System;
using GreetingsEntities;

namespace GreetingsInteractors.Responses
{
    public class FindPersonResult
    {
         public string Name { get; private set; }
         public FindPersonResult(Person person)
        {
            Name = person.Name;
        }

   }
}
