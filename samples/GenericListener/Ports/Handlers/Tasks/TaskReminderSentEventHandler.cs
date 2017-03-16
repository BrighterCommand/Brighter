using GenericListener.Ports.Indexers;
using log4net;
using Paramore.Brighter;
using Tasks.Ports.Events;

namespace GenericListener.Ports.Handlers.Tasks
{
    public class TaskReminderSentEventHandler : RequestHandler<TaskReminderSentEvent>
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(TaskReminderSentEventHandler));

        private readonly ITaskReminderSentEventIndexer _indexer;

        public TaskReminderSentEventHandler(ITaskReminderSentEventIndexer indexer)
        {
            _indexer = indexer;
        }

        public override TaskReminderSentEvent Handle(TaskReminderSentEvent command)
        {
            _logger.InfoFormat("Received TaskReminderSentEvent {0}", command.Id);

            _indexer.Index(command);

            return base.Handle(command);
        }
    }
}