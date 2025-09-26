using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalutationApp.Entities;

namespace SalutationApp.EntityGateway
{
    public class SalutationEntityTypeConfiguration : IEntityTypeConfiguration<Salutation>
    {
        public void Configure(EntityTypeBuilder<Salutation> builder)
        {
            builder.ToTable("Salutation");
            builder.HasKey("Id");
            builder.Property("Id");
            builder.Property(g => g.Greeting)
                .HasMaxLength(255)
                .IsRequired();
        }
    }
}
