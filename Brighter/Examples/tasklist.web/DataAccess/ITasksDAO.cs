using tasklist.web.Models;

namespace tasklist.web.DataAccess
{
    public interface ITasksDAO
    {
        void Add(Task newTask);
        dynamic Db { get; set; }
    }
}