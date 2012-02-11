using tasklist.web.Models;

namespace tasklist.web.Tests
{
    public interface ITaskRetriever
    {
        Task Get(int taskId);
    }
}