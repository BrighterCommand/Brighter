using GreetingsApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreetingsApp.EntityGateway
{
    public class PersonEntityTypeConfiguration : IEntityTypeConfiguration<Person>
    {
        public void Configure(EntityTypeBuilder<Person> builder)
        {
            builder.ToTable("Person");
            builder.HasKey("Id").HasName("PRIMARY");
            builder.Property("Id");
            
            
            builder.Property(p => p.TimeStamp).IsRowVersion();
            builder.Property(p => p.Name).IsRequired();
            builder.HasAlternateKey(p => p.Name);
        }
    }
}
