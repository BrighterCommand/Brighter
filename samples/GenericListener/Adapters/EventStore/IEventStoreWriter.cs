using System;
using EventStore.ClientAPI;
using Paramore.Brighter;

namespace GenericListener.Adapters.EventStore
{
    /// <summary>
    /// EventStoreWriter adapter with extensive customizability.
    /// </summary>
    /// <typeparam name="TEvent">The event object to process, and which to store (or store a property of).</typeparam>
    public interface IEventStoreWriter<TEvent> where TEvent : IRequest
    {
        /// <summary>
        /// Initialize the writer.
        /// </summary>
        /// <param name="connection">IEventStore connection</param>
        /// <param name="stream">Delegate returning stream name (optionally using processing TEvent)</param>
        /// <param name="eventStoreId">Delegate returning EventStoreId (optionally using processing TEvent)</param>
        /// <param name="eventType">Delegate returning EventStore's EventType (optionally using processing TEvent)</param>
        /// <param name="metaData">Delegate returning EventStore metadata object (optionally using processing TEvent)</param>
        /// <param name="eventData">Delegate returning EventStore data object (usually TEvent or a property of)</param>
        /// <returns></returns>
        EventStoreWriter<TEvent> Initialize(IEventStoreConnection connection, Func<TEvent, string> stream, Func<TEvent, Guid> eventStoreId, Func<TEvent, string> eventType,
            Func<TEvent, object> metaData, Func<TEvent, object> eventData);

        /// <summary>
        /// Initiaze the writer.
        /// </summary>
        /// <param name="eventStoreAction"></param>
        /// <param name="stream">Delegate returning stream name (optionally using processing TEvent)</param>
        /// <param name="eventStoreId">Delegate returning EventStoreId (optionally using processing TEvent)</param>
        /// <param name="eventType">Delegate returning EventStore's EventType (optionally using processing TEvent)</param>
        /// <param name="metaData">Delegate returning EventStore metadata object (optionally using processing TEvent)</param>
        /// <param name="eventData">Delegate returning EventStore data object (usually TEvent or a property of)</param>
        /// <returns></returns>
        EventStoreWriter<TEvent> Initialize(Action<TEvent, Action<IEventStoreConnection>> eventStoreAction, Func<TEvent, string> stream, Func<TEvent, Guid> eventStoreId, Func<TEvent, string> eventType, 
            Func<TEvent, object> metaData, Func<TEvent, object> eventData);

        /// <summary>
        /// Returns current initialized state
        /// </summary>
        bool Initialized { get; }

        /// <summary>
        /// Write event into EventStore
        /// </summary>
        /// <param name="event"></param>
        void Write(TEvent @event);
    }
}
