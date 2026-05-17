using GreetingsApp.Entities;

namespace GreetingsApp.Responses
{
    public class FindPersonResult
    {
        public FindPersonResult() { /*serialization constructor*/ }
         public string Name { get; private set; }
         public FindPersonResult(Person person)
        {
            Name = person.Name;
        }

   }
}
