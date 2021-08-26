using System;
using GreetingsEntities;

namespace GreetingsInteractors.Responses
{
    public class FindPersonResult
    {
         public Guid Id { get; set; }
         public string Name { get; private set; }
         public FindPersonResult(Person person)
        {
            Id = person.Id;
            Name = person.Name;
        }

   }
}
