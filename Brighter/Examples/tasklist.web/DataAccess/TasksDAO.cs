using tasklist.web.Models;

namespace tasklist.web.DataAccess
{
    public class TasksDAO : ITasksDAO
    {
        public dynamic Db { get; set; }

        public TasksDAO() {}

        //inserting the id is problematic, but against SQLite Simple.Data does not return the row we inserted

        public void Add(Task newTask)
        {
            Db.Tasks.Insert(id:newTask.Id, taskname: newTask.TaskName, taskdescription: newTask.TaskDescription);
        }

        public void Clear()
        {
            Db.Tasks.DeleteAll();
        }
      }
}