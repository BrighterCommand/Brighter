using Tasklist.Domain;

namespace Tasklist.Adapters.DataAccess
{
    public interface ITasksDAO
    {
        Task Add(Task newTask);
        Task FindById(int id);
        void Update(Task task);

    }
}