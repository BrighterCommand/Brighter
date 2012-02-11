using Simple.Data;
using tasklist.web.Models;

namespace tasklist.web.ViewModelRetrievers
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