using GreetingsApp.Entities;

namespace GreetingsApp.Responses
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
