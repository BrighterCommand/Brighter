using Microsoft.EntityFrameworkCore;
using SalutationEntities;

namespace SalutationPorts.EntityGateway
{
    public class SalutationsEntityGateway : DbContext
    {
        public SalutationsEntityGateway (DbContextOptions<SalutationsEntityGateway > options) : base(options) { }
        
        public DbSet<Salutation> Salutations { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new SalutationEntityTypeConfiguration().Configure(modelBuilder.Entity<Salutation>());

            base.OnModelCreating(modelBuilder);
        }
         
    }
}
