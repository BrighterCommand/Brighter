using System.Data;
using DapperExtensions.Mapper;
using GreetingsEntities;

namespace GreetingsPorts.EntityMappers;

public class GreetingsMapper : ClassMapper<Greeting>
{
    public GreetingsMapper()
    {
        TableName = nameof(Person);
        Map(g=> g.Id).Column("Id").Key(KeyType.Identity);
        Map(g => g.Message).Column("Message");
        Map(g => g.RecipientId).Column("RecipientId").Key(KeyType.ForeignKey);
        ReferenceMap(g => g.Recipient).Reference<Person>((p, g) => p.Id == g.RecipientId );
    }
    
}
