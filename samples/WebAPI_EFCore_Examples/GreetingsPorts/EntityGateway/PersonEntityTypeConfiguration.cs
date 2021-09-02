using GreetingsEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreetingsInteractors.EntityGateway
{
    public class PersonEntityTypeConfiguration : IEntityTypeConfiguration<Person>
    {
        public void Configure(EntityTypeBuilder<Person> builder)
        {
            builder.HasKey("_id");
            builder.Property("_id");
            builder.HasAlternateKey(p => p.Name);
            builder.Property(p => p.TimeStamp).IsRowVersion();
            builder.Property(p => p.Name).IsRequired();
            builder.HasMany(p => p.Greetings);
        }
    }
}
