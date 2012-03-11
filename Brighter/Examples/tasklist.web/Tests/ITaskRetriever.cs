using tasklist.web.Models;

namespace tasklist.web.Tests
{
    public interface ITaskRetriever
    {
        TaskModel Get(int taskId);
    }
}