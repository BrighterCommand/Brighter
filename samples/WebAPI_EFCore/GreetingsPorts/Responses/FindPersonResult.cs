using GreetingsEntities;

namespace GreetingsPorts.Responses
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
