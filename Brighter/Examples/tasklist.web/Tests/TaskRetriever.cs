using Simple.Data;
using tasklist.web.Models;
using tasklist.web.ViewModelRetrievers;

namespace tasklist.web.Tests
{
    internal class TaskRetriever : SimpleDataRetriever, ITaskRetriever
    {
        public TaskModel Get(int taskId)
        {
            var db = Database.Opener.OpenFile(DatabasePath);
            var matchingTask = db.Tasks.QueryById(taskId)
                .Select(db.Tasks.TaskName, db.Tasks.TaskDescription, db.Tasks.DueDate);
            return new TaskModel(taskName: matchingTask.TaskName, taskDescription: matchingTask.TaskDescription, dueDate: matchingTask.DueDate.ToString());
        }
    }
}