using Simple.Data;
using Tasklist.Adapters.API.Resources;

namespace Tasklist.Ports.ViewModelRetrievers
{
    internal class TaskRetriever : SimpleDataRetriever, ITaskRetriever
    {
        public TaskModel Get(int taskId)
        {
            var db = Database.Opener.OpenFile(DatabasePath);
            var matchingTask = db.Tasks.QueryById(taskId)
                .Select(db.Tasks.TaskName, db.Tasks.TaskDescription, db.Tasks.DueDate)
                .Single();
            string dueDate = matchingTask.duedate != null ? matchingTask.duedate.ToString() : string.Empty;
            return new TaskModel(dueDate: dueDate, taskName: matchingTask.taskname, taskDescription: matchingTask.taskdescription);
        }
    }
}