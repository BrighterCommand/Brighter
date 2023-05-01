using DapperExtensions.Mapper;
using SalutationEntities;

namespace SalutationPorts.EntityMappers;

public class SalutationMapper : ClassMapper<Salutation>
{
    public SalutationMapper()
    {
        TableName = nameof(Salutation);
        Map(s => s.Id).Column("Id").Key(KeyType.Identity);
        Map(s => s.Greeting).Column("Greeting");
        Map(s => s.TimeStamp).Column("TimeStamp").Ignore();
    }
}
