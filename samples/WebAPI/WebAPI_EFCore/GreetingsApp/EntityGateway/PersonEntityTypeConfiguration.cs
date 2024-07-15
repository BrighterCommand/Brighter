using GreetingsApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreetingsApp.EntityGateway
{
    public class PersonEntityTypeConfiguration : IEntityTypeConfiguration<Person>
    {
        public void Configure(EntityTypeBuilder<Person> builder)
        {
            builder.HasKey("Id");
            builder.Property("Id");
            builder.HasAlternateKey(p => p.Name);
            builder.Property(p => p.TimeStamp).IsRowVersion();
            builder.Property(p => p.Name).IsRequired();
            builder.HasMany(p => p.Greetings);
        }
    }
}
