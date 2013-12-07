using System.IO;
using System.Reflection;
using Simple.Data;
using Tasklist.Domain;

namespace Tasklist.Adapters.DataAccess
{
    public class TasksDAO : ITasksDAO
    {
        static readonly string DatabasePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase.Substring(8)),"tasks.sqlite");

        private dynamic db;

        public TasksDAO()
        {
            db = Database.Opener.OpenFile(DatabasePath);
        }

        //inserting the id is problematic, but against SQLite Simple.Data does not return the row we inserted,
        //and we want to know to test integration. Trick might be to rewrite with a different Db.

        internal void Add(int id, Task newTask)
        {
            db.Tasks.Insert(id:id, taskname: newTask.TaskName, taskdescription: newTask.TaskDescription, DueDate: newTask.DueDate);
        }

        public void Add(Task newTask)
        {
            db.Tasks.Insert(taskname: newTask.TaskName, taskdescription: newTask.TaskDescription, DueDate: newTask.DueDate);
        }

        public void Update(Task task)
        {
            db.Tasks.UpdateById(task);
        }

        public void Clear()
        {
            db.Tasks.DeleteAll();
        }

        public Task FindById(int taskId)
        {
            return db.Tasks.FindById(taskId);
        }

        public Task FindByName(string taskName)
        {
            return db.Tasks.FindBy(taskName: taskName);
        }

    }
}