namespace Tasklist.Adapters.Tests
{
    public interface ITaskRetriever
    {
        TaskModel Get(int taskId);
    }
}