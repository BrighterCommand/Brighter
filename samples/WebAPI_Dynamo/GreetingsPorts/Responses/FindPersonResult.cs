using GreetingsEntities;

namespace GreetingsPorts.Responses
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
