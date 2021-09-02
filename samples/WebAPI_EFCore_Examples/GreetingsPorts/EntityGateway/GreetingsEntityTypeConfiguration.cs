using GreetingsEntities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreetingsInteractors.EntityGateway
{
    public class GreetingsEntityTypeConfiguration : IEntityTypeConfiguration<Greeting>
    {
        public void Configure(EntityTypeBuilder<Greeting> builder)
        {
            builder.HasKey("_id");
            builder.Property("_id");
            builder.Property(g => g.Message).IsRequired();
            builder.HasOne(g => g.Recipient);
        }
    }
}
