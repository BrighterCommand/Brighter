using GreetingsApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreetingsApp.EntityGateway
{
    public class GreetingsEntityTypeConfiguration : IEntityTypeConfiguration<Greeting>
    {
        public void Configure(EntityTypeBuilder<Greeting> builder)
        {
            builder.HasKey("Id");
            builder.Property("Id");
            builder.Property(g => g.Message).IsRequired();
            builder.HasOne(g => g.Recipient);
        }
    }
}
