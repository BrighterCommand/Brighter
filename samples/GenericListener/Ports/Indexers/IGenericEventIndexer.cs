using GenericListener.Ports.Events;

namespace GenericListener.Ports.Indexers
{
    public interface IGenericFeedEventIndexer<in T> where T : EventStoredEvent
    {
        void Index(T @event);
    }
}