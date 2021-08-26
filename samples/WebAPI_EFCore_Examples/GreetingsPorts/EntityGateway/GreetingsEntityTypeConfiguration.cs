using GreetingsEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreetingsInteractors.EntityGateway
{
    public class GreetingsEntityTypeConfiguration : IEntityTypeConfiguration<Greeting>
    {
        public void Configure(EntityTypeBuilder<Greeting> builder)
        {
            builder
                .HasKey(g => g.Id);
            builder
                .Property(g => g.Message)
                .IsRequired();
        }
    }
}
