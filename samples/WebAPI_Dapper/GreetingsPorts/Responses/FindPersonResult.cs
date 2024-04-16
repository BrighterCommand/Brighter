using GreetingsEntities;

namespace GreetingsPorts.Responses
{
    public class FindPersonResult
    {
         public Person Person { get; private set; }
         public FindPersonResult(Person person)
        {
            Person = person;
        }

   }
}
