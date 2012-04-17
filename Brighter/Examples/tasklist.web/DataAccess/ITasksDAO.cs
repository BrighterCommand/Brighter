using tasklist.web.Models;

namespace tasklist.web.DataAccess
{
    public interface ITasksDAO
    {
        void Add(Task newTask);
        Task FindById(int id);
        void Update(Task task);

    }
}