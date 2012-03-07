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

        //inserting the id is problematic, but against SQLite Simple.Data does not return the row we inserted

        public void Add(Task newTask)
        {
            db.Tasks.Insert(id:newTask.Id, taskname: newTask.TaskName, taskdescription: newTask.TaskDescription);
        }

        public void Clear()
        {
            db.Tasks.DeleteAll();
        }
      }
}