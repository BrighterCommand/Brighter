using Tasks.Ports.Events;

namespace GenericListener.Ports.Indexers
{
    public interface ITaskReminderSentEventIndexer
    {
        void Index(TaskReminderSentEvent @event);
    }
}