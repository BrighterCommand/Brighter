using Simple.Data;
using tasklist.web.Models;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Tests
{
    internal class TaskRetriever : SimpleDataRetriever, ITaskRetriever
    {
        public Task Get(int taskId)
        {
            var db = Database.Opener.OpenFile(DatabasePath);
            var task = db.Tasks.FindById(taskId);
            return task;
        }
    }
}