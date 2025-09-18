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
            
            builder.Property(g => g.RecipientId)
                .HasColumnName("Recipient_Id");

            builder
                .HasOne(g => g.Recipient)
                .WithMany()
                .HasForeignKey(g => g.RecipientId);
        }
    }
}
