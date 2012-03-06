using tasklist.web.Models;

namespace tasklist.web.DataAccess
{
    public class TasksDAO : ITasksDAO
    {
        private readonly dynamic db;

        public TasksDAO(dynamic db)
        {
            this.db = db;
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