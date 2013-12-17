using Tasklist.Adapters.API.Resources;

namespace Tasklist.Ports.ViewModelRetrievers
{
    public interface ITaskRetriever
    {
        TaskModel Get(int taskId);
    }
}