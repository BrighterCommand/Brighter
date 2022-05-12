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
        //Map(g => g.Recipient).Ignore();
        Map(g => g.RecipientId).Column("RecipientId").Key(KeyType.ForeignKey);
    }
    
}

