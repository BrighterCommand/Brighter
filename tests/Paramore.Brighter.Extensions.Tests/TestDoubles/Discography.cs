using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

public class Album(string title, string artist)
{
    public int? Id { get; set; }
    public string Title { get; set; } = title;
    public string Artist { get; set; } = artist;
}

public class Discography : DbContext
{
    public DbSet<Album> Albums { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlite("Data Source=discography.db;Cache=Shared");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>()
            .Property(s => s.Id)
            .ValueGeneratedOnAdd();
    }
}
