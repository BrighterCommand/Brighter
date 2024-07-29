using GreetingsEntities;

namespace GreetingsApp.Responses
{
    public class FindPersonResult
    {
         public string Name { get; set; }

         public FindPersonResult() { }

         public FindPersonResult(Person person)
        {
            Name = person.Name;
        }

   }
}
