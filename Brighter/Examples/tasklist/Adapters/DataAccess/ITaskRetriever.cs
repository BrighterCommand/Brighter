using Tasklist.Adapters.API.Resources;

namespace Tasklist.Adapters.DataAccess
{
    public interface ITaskRetriever
    {
        TaskModel Get(int taskId);
    }
}