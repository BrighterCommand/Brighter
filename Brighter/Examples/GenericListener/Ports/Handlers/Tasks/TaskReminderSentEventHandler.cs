using GenericListener.Ports.Indexers;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using Tasks.Ports.Events;

namespace GenericListener.Ports.Handlers.Tasks
{
    public class TaskReminderSentEventHandler : RequestHandler<TaskReminderSentEvent>
    {
        private readonly ITaskReminderSentEventIndexer _indexer;

        public TaskReminderSentEventHandler(ITaskReminderSentEventIndexer indexer)
        {
            _indexer = indexer;
        }

        public override TaskReminderSentEvent Handle(TaskReminderSentEvent command)
        {
            Logger.InfoFormat("Received TaskReminderSentEvent {0}", command.Id);

            _indexer.Index(command);

            return base.Handle(command);
        }
    }
}