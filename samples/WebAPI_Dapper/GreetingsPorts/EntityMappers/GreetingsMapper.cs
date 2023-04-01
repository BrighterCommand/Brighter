using System.Data;
using DapperExtensions.Mapper;
using GreetingsEntities;

namespace GreetingsPorts.EntityMappers;

public class GreetingsMapper : ClassMapper<Greeting>
{
    public GreetingsMapper()
    {
        TableName = nameof(Greeting);
        Map(g=> g.Id).Column("Id").Key(KeyType.Identity);
        Map(g => g.Message).Column("Message");
        Map(g => g.RecipientId).Column("Recipient_Id").Key(KeyType.ForeignKey);
    }

}

