using System;
using EventStore.ClientAPI;
using GenericListener.Adapters.EventStore;
using GenericListener.Ports.Events;

namespace GenericListener.Ports.Indexers
{
    public class GenericEventIndexer<T> : IGenericFeedEventIndexer<T> where T : EventStoredEvent
    {
        private readonly Func<T, string> _stream = e => string.Format("{0}-{1}", e.Context, e.CategoryId);
        private readonly IEventStoreWriter<T> _eventStoreWriter;

        public GenericEventIndexer(IEventStoreConnection eventStoreConnection, IEventStoreWriter<T> eventStoreWriter)
        {
            _eventStoreWriter = eventStoreWriter;

            if (!_eventStoreWriter.Initialized)
            {
                _eventStoreWriter.Initialize(
                    connection: eventStoreConnection,
                    stream: _stream,
                    eventStoreId: e => e.Id,
                    eventType: e => e.GetType().FullName,
                    metaData: e => e.MetaData,
                    eventData: e => e.JsonData);

                //// Alternative we could control the write action to do custom operations
                //// at point of writing, such as set stream metadata should the stream not
                //// already exist.
                //
                //_eventStoreWriter.Initialize(
                //    eventStoreAction: (e, writeAction) =>
                //    {
                //        eventStoreConnection.SetStreamMetadataAsync(
                //            _stream.Invoke(e),
                //            ExpectedVersion.Any,
                //            StreamMetadata.Build()
                //                .SetReadRole("$all")
                //                .SetWriteRole("$all")
                //                .SetDeleteRole("$all")
                //                .SetMetadataReadRole("$all")
                //                .SetMetadataWriteRole("$all")).Wait();

                //        writeAction.Invoke(eventStoreConnection);
                //    },
                //    stream: _stream,
                //    eventStoreId: e => e.Id,
                //    eventType: e => e.GetType().FullName,
                //    metaData: e => e.MetaData,
                //    eventData: e => e.JsonData);
            }
        }

        public void Index(T @event)
        {
            _eventStoreWriter.Write(@event);
        }
    }
}