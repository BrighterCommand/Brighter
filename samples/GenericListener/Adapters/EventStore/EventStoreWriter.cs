using System;
using System.Text;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Paramore.Brighter;

namespace GenericListener.Adapters.EventStore
{
    public sealed class EventStoreWriter<TEvent> : IEventStoreWriter<TEvent> where TEvent : IRequest
    {

        private readonly object _lockObject = new object();

        private volatile bool _initialized;

        private Func<TEvent, string> _getEventType; 

        private Func<TEvent, object> _createMetaData;

        private Func<TEvent, object> _createEventData; 

        private Func<TEvent, Guid> _getEventStoreId;

        public EventStoreWriter()
        {
            this._getEventType = @event => @event.GetType().FullName;
            this._createMetaData = @event => new { Created = DateTime.Now, Type = @event.GetType().Name };
            this._createEventData = @event => @event;
        }

        private Action<TEvent, Action<IEventStoreConnection>> _eventStoreAction;

        private Func<TEvent, string> Stream { get; set; }

        public bool Initialized { get { return _initialized; } }

        public EventStoreWriter<TEvent> Initialize(IEventStoreConnection connection, Func<TEvent, string> stream, Func<TEvent, Guid> eventStoreId, Func<TEvent, string> eventType, Func<TEvent, object> metaData, Func<TEvent, object> eventData)//, string defaultDataEventIdPropertyName = "Id")
        {
            return this.Initialize(
                (@event, writeAction) => writeAction.Invoke(connection),
                stream,
                eventStoreId,
                eventType,
                metaData,
                eventData);
        }

        public EventStoreWriter<TEvent> Initialize(Action<TEvent, Action<IEventStoreConnection>> eventStoreAction, Func<TEvent, string> stream, Func<TEvent, Guid> eventStoreId, Func<TEvent, string> eventType, Func<TEvent, object> metaData, Func<TEvent, object> eventData)//, string defaultDataEventIdPropertyName = "Id")
        {
            if (!this._initialized)
            {
                lock (this._lockObject)
                {
                    if (!this._initialized)
                    {
                        this.Stream = stream;

                        this._getEventType = eventType;

                        this._createMetaData = metaData;

                        this._createEventData = eventData;

                        this._eventStoreAction = eventStoreAction;

                        this._getEventStoreId = eventStoreId;

                        this._initialized = true;
                    }
                }
            }
            else
            {
                throw new Exception("Writer has already been initialized. Expected static/singleton usage.");
            }

            return this;
        }

        public void Write(TEvent @event)
        {
            if (!this._initialized)
            {
                throw new Exception("Writer has not been initialized.");
            }

            var eventType = _getEventType(@event);
            var eventStoreId = this._getEventStoreId.Invoke(@event);

            if (eventStoreId == Guid.Empty)
            {
                throw new Exception("Mapped EventStoreId Guid is invalid (empty).");
            }

            var eventData = this._createEventData(@event);
            var metaData = this._createMetaData(@event);

            var eventDataIsJson = eventData.GetType() == typeof(byte[]);
            var metaDataIsJson = metaData.GetType() == typeof(byte[]);

            var encodedData = eventDataIsJson ? (byte[])eventData : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventData));
            var encodedMetaData = metaDataIsJson ? (byte[])metaData : Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(metaData));

            var eventStoreData = new EventData(eventStoreId, eventType, true, encodedData, encodedMetaData);

            this._eventStoreAction.Invoke(@event, eventStoreConnection => eventStoreConnection.AppendToStreamAsync(this.Stream.Invoke(@event), ExpectedVersion.Any, eventStoreData).Wait());
        }
    }
}