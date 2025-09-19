using GreetingsApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GreetingsApp.EntityGateway
{
    public class GreetingsEntityTypeConfiguration : IEntityTypeConfiguration<Greeting>
    {
        public void Configure(EntityTypeBuilder<Greeting> builder)
        {
            builder.ToTable("Greeting");
            builder.HasKey("Id").HasName("PRIMARY");
            builder.Property("Id");
            builder.Property(g => g.Message)
                .HasMaxLength(255)
                .IsRequired();
            
            builder.Property(g => g.RecipientId)
                .HasColumnName("Recipient_Id");

            builder
                .HasOne(g => g.Recipient)
                .WithMany(p => p.Greetings)
                .HasForeignKey(g => g.RecipientId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        }
    }
}
