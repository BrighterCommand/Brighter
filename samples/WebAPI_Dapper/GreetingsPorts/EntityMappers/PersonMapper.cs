using DapperExtensions.Mapper;
using GreetingsEntities;

namespace GreetingsPorts.EntityMappers;

    public class PersonMapper : ClassMapper<Person>
    {
        public PersonMapper()
        {
            TableName = nameof(Person);
            Map(p => p.Id).Column("Id").Key(KeyType.Identity);
            Map(p => p.Name).Column("Name");
            Map(p => p.TimeStamp).Column("TimeStamp").Ignore();
            Map(p => p.Greetings).Ignore();
            ReferenceMap(p => p.Greetings).Reference<Greeting>((g, p) => g.RecipientId == p.Id);
        }
    }
