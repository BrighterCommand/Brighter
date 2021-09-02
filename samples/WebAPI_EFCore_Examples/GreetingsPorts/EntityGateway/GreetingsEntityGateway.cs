﻿using GreetingsEntities;
using Microsoft.EntityFrameworkCore;

namespace GreetingsPorts.EntityGateway
{
    public class GreetingsEntityGateway : DbContext
    {
        public GreetingsEntityGateway(DbContextOptions<GreetingsEntityGateway> options) : base(options) { }

        public DbSet<Person> People { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new PersonEntityTypeConfiguration().Configure(modelBuilder.Entity<Person>());
            new GreetingsEntityTypeConfiguration().Configure(modelBuilder.Entity<Greeting>());

            base.OnModelCreating(modelBuilder);
        }
    }
}
