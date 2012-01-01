using System.IO;
using System.Reflection;
using Simple.Data;
using tasklist.web.Models;

namespace tasklist.web.ViewModelRetrievers
{
    public class TaskListRetriever : ITaskListRetriever
    {
        static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");

        public dynamic RetrieveTasks()
        {
            var db = Database.Opener.OpenFile(DatabasePath);
            var tasks = db.Tasks.All().ToList<Task>();
            return tasks;
        }
    }
}