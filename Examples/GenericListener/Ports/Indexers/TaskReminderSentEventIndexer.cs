using System;
using EventStore.ClientAPI;
using GenericListener.Adapters.EventStore;
using Tasks.Ports.Events;

namespace GenericListener.Ports.Indexers
{
    public class TaskReminderSentEventIndexer : ITaskReminderSentEventIndexer
    {
        private readonly IEventStoreWriter<TaskReminderSentEvent> _eventStoreWriter;

        public TaskReminderSentEventIndexer(IEventStoreConnection eventStoreConnection, IEventStoreWriter<TaskReminderSentEvent> eventStoreWriter)
        {
            _eventStoreWriter = eventStoreWriter;

            if (!_eventStoreWriter.Initialized)
            {
                _eventStoreWriter.Initialize(
                    connection: eventStoreConnection,
                    stream: e => string.Format("Task-{0}", e.TaskId),
                    eventStoreId: e => e.Id,
                    eventType: e => e.GetType().ToString(),
                    metaData: e => new { IndexedCreatedDate = DateTime.UtcNow },
                    eventData: e => e);
            }
        }

        public void Index(TaskReminderSentEvent @event)
        {
            _eventStoreWriter.Write(@event);
        }
    }
}