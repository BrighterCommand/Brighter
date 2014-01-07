using OpenRasta.Web;
using Tasklist.Ports.ViewModelRetrievers;

namespace Tasklist.Adapters.API.Handlers
{
    public class TaskListEndPointHandler
    {
        private readonly ITaskListRetriever taskListRetriever;

        public TaskListEndPointHandler(ITaskListRetriever taskListRetriever)
        {
            taskListRetriever = taskListRetriever;
        }

        public OperationResult Get()
        {
            return new OperationResult.OK{ResponseResource = taskListRetriever.RetrieveTasks()}
        }
    }
}