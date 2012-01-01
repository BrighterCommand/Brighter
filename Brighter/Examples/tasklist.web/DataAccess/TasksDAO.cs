using System.IO;
using System.Reflection;
using Simple.Data;
using tasklist.web.Models;

namespace tasklist.web.DataAccess
{
    public class TasksDAO : ITasksDAO
    {
        static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");
        private dynamic db;

        public TasksDAO()
        {
            db = Database.Opener.OpenFile(DatabasePath);
        }

        public void Add(Task newTask)
        {
            db.Tasks.Insert(taskname: newTask.TaskName, taskdescription: newTask.TaskDescription);
        }

        public void Clear()
        {
            db.Tasks.DeleteAll();
        }
    }
}