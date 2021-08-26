using GreetingsEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreetingsInteractors.EntityGateway
{
    public class PersonEntityTypeConfiguration : IEntityTypeConfiguration<Person>
    {
        public void Configure(EntityTypeBuilder<Person> builder)
        {
            builder
                .HasKey(p => p.Id);

            builder
                .HasAlternateKey(p => p.Name);
            
            builder
                .Property(p => p.TimeStamp)
                .IsRowVersion();

            builder
                .Property(p => p.Name)
                .IsRequired();
        }
    }
}
