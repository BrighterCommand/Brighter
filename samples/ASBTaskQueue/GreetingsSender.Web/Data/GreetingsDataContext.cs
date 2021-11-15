using Microsoft.EntityFrameworkCore;
using Greetings.Ports.Events;

namespace GreetingsSender.Web.Data
{
    public class GreetingsDataContext : DbContext
    {
        
        public DbSet<GreetingEvent> Greetings { get; set; }
        public DbSet<GreetingAsyncEvent> GreetingsAsync { get; set; }

        public GreetingsDataContext(DbContextOptions options) : base(options)
        {
        }
    }
}
