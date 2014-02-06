using Simple.Data;
using Tasklist.Adapters.API.Resources;
using Tasklist.Domain;

namespace Tasklist.Ports.ViewModelRetrievers
{
    public class TaskListRetriever : SimpleDataRetriever, ITaskListRetriever
    {
        public dynamic RetrieveTasks()
        {
            var db = Database.Opener.OpenFile(DatabasePath);
            var tasks = db.Tasks.All().ToList<Task>();
            var taskList = new TaskListModel(tasks);
            return taskList ;
        }
    }
}