using Microsoft.EntityFrameworkCore;
using Tasks.Model;

namespace Tasks.Adapters.DataAccess
{
    /// <summary>
    /// The entity framework context with a Task DbSet 
    /// </summary>
    public class TasksContext : DbContext
    {
        public TasksContext(DbContextOptions<TasksContext> options)
            : base(options)
        { }

        public DbSet<Task> Tasks { get; set; }
    }
}