using Microsoft.EntityFrameworkCore;

namespace Tasks.Adapters.DataAccess
{
    /// <summary>
    /// A factory to create an instance of the TasksContext 
    /// </summary>
    public static class TasksContextFactory
    {
        public static TasksContext Create(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TasksContext>();
            optionsBuilder.UseSqlite(connectionString);

            // Ensure that the SQLite database and schema is created!
            var context = new TasksContext(optionsBuilder.Options);
            context.Database.EnsureCreated();

            return context;
        }
    }
}
