using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalutationApp.Entities;

namespace SalutationApp.EntityGateway
{
    public class SalutationEntityTypeConfiguration : IEntityTypeConfiguration<Salutation>
    {
        public void Configure(EntityTypeBuilder<Salutation> builder)
        {
            builder.HasKey("_id");
            builder.Property("_id");
            builder.Property(g => g.Greeting).IsRequired();
        }
    }
}
