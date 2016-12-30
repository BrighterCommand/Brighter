using System;
using paramore.brighter.commandprocessor;

namespace GenericListener.Ports.Events
{
    public class EventStoredEvent : IRequest
    {
        /// <summary>
        /// EventStore requireds a Guid for each event written.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Commonly used to imply or construct the stream name (e.g. Tasks).
        /// </summary>
        public string Context { get; set; }

        /// <summary>
        /// Commonly used for category-based stream names (e.g. Tasks-20).
        /// </summary>
        public string CategoryId { get; set; }

        /// <summary>
        /// The EventStoreWriter will not serialize any byte[] fields
        /// and assume they are already JSON ready for encoding.
        /// </summary>
        public byte[] JsonData { get; set; }

        /// <summary>
        /// MetaData to write with the event. As this is an object it will
        /// be serialized it into JSON before encoding.
        /// </summary>
        public object MetaData { get; set; }
    }
}