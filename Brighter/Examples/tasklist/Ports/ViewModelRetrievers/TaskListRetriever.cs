using Simple.Data;
using Tasklist.Domain;

namespace Tasklist.Ports.ViewModelRetrievers
{
    public class TaskListRetriever : SimpleDataRetriever, ITaskListRetriever
    {
        public dynamic RetrieveTasks()
        {
            var db = Database.Opener.OpenFile(DatabasePath);
            var tasks = db.Tasks.All().ToList<Task>();
            return tasks;
        }
    }
}