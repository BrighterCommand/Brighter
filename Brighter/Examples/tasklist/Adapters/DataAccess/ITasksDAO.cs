using Tasklist.Domain;

namespace Tasklist.Adapters.DataAccess
{
    public interface ITasksDAO
    {
        void Add(Task newTask);
        Task FindById(int id);
        void Update(Task task);

    }
}