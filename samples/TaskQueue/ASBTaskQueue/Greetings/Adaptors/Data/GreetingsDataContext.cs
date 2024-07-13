using Greetings.Ports.Entities;
using Greetings.Ports.Events;
using Microsoft.EntityFrameworkCore;

namespace Greetings.Adaptors.Data
{
    public class GreetingsDataContext : DbContext
    {
        
        public DbSet<GreetingEvent> Greetings { get; set; }
        public DbSet<GreetingAsyncEvent> GreetingsAsync { get; set; }
        
        public DbSet<Greeting> GreetingsRegister { get; set; }

        public GreetingsDataContext(DbContextOptions options) : base(options)
        {
        }
    }
}
