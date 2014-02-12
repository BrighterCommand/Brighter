using OpenRasta.Web;
using Simple.Data;
using Tasklist.Adapters.API.Resources;
using Tasklist.Domain;

namespace Tasklist.Ports.ViewModelRetrievers
{
    public class TaskListRetriever : SimpleDataRetriever, ITaskListRetriever
    {
        private string hostName;

        public TaskListRetriever(ICommunicationContext context)
        {
            hostName = context.ApplicationBaseUri.Host;
        }

        public TaskListRetriever(string hostName)
        {
            this.hostName = hostName;
        }

        public dynamic RetrieveTasks()
        {
            var db = Database.Opener.OpenFile(DatabasePath);
            var tasks = db.Tasks.All().ToList<Task>();
            var taskList = new TaskListModel(tasks, hostName);
            return taskList ;
        }
    }
}