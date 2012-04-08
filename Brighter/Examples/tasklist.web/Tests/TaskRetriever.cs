using System;
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
            string dueDate = matchingTask.DueDate != null ? matchingTask.DueDate.ToUniversalTime().ToString() : string.Empty;
            return new TaskModel(dueDate: dueDate, taskName: matchingTask.TaskName, taskDescription: matchingTask.TaskDescription);
        }
    }
}