#region Licence

/* The MIT License (MIT)
Copyright © 2015 George Ayris <george.ayris@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Paramore.Brighter.Logging;

namespace Paramore.Brighter.Outbox.EventStore
{
    /// <summary>
    ///     Class EventStoreOutbox.
    /// </summary>
    public class EventStoreOutbox : 
        IAmAnOutbox<Message>, 
        IAmAnOutboxAsync<Message>,
        IAmAnOutboxViewer<Message>,
        IAmAnOutboxViewerAsync<Message>
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<EventStoreOutbox>);

        private readonly IEventStoreConnection _eventStore;

        /// <summary>
        /// If false we the default thread synchronization context to run any continuation, if true we re-use the original synchronization context.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait 
        /// or access the Result or otherwise block. You may need the orginating synchronization context if you need to access thread specific storage
        /// such as HTTPContext 
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EventStoreOutbox" /> class.
        /// </summary>
        /// <param name="eventStore">The active connection to an Event Store instance.</param>
        public EventStoreOutbox(IEventStoreConnection eventStore)
        {
            _eventStore = eventStore;
        }

        /// <summary>
        ///     Adds the specified message.
        ///     The message must have a 'streamId' and an 'eventNumber' in the message header bag.
        ///     The 'streamId' is the name of the stream to append the message to.
        ///     The 'eventNumber' should be one greater than the last event in the stream.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout">The outBoxTimeout.</param>
        /// <returns>Task.</returns>
        public void Add(Message message, int outBoxTimeout = -1)
        {
            _logger.Value.DebugFormat("Adding message to Event Store Outbox: {0}", JsonConvert.SerializeObject(message));

            var headerBag = message.Header.Bag;
            var streamId = ExtractStreamIdFromHeader(headerBag, message.Id);
            var eventNumber = ExtractEventNumberFromHeader(headerBag, message.Id);
            var numberOfPreviousEvent = eventNumber - 1;
            var eventData = CreateEventData(message, headerBag);

            _eventStore.AppendToStreamAsync(streamId, numberOfPreviousEvent, eventData).Wait();
        }


        /// <summary>
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="outBoxTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task"/>.</returns>
        public async Task AddAsync(Message message, int outBoxTimeout = -1,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            _logger.Value.DebugFormat("Adding message to Event Store Outbox: {0}", JsonConvert.SerializeObject(message));

            var headerBag = message.Header.Bag;
            var streamId = ExtractStreamIdFromHeader(headerBag, message.Id);
            var eventNumber = ExtractEventNumberFromHeader(headerBag, message.Id);
            var numberOfPreviousEvent = eventNumber - 1;
            var eventData = CreateEventData(message, headerBag);

            await _eventStore.AppendToStreamAsync(streamId, numberOfPreviousEvent, eventData);
        }

       /// <summary>
        /// Get the messages that have been marked as flushed in the store
        /// </summary>
        /// <param name="millisecondsDispatchedSince">How long ago would the message have been dispatched in milliseconds</param>
        /// <param name="pageSize">How many messages in a page</param>
        /// <param name="pageNumber">Which page of messages to get</param>
        /// <param name="outboxTimeout"></param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of dispatched messages</returns>
        public IEnumerable<Message> DispatchedMessages(
            double millisecondsDispatchedSince, 
            int pageSize = 100, 
            int pageNumber = 1, 
            int outboxTimeout = -1,
            Dictionary<string, object> args = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets all messages in the OutBox, LIFO
        /// </summary>
        /// <param name="pageSize">number of items on the page, default is 100</param>
        /// <param name="pageNumber">page number of results to return, default is first</param>
        /// <param name="args">Additional parameters required for search, if any</param>
         /// <returns></returns>
        public IList<Message> Get(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null)
        {
            throw new NotImplementedException();
        }

        public Task<IList<Message>> GetAsync(int pageSize = 100, int pageNumber = 1, Dictionary<string, object> args = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        ///     Gets the specified message by identifier. Currently not implemented.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The outBoxTimeout.</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public Message Get(Guid messageId, int outBoxTimeout = -1)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Returns multiple events from a given stream.
        ///     If all the events do not exist, as many as can be found will be returned.
        /// </summary>
        /// <param name="stream">The stream name.</param>
        /// <param name="fromEventNumber">The event number to start from (inclusive).</param>
        /// <param name="numberOfEvents">The number of events to return.</param>
        /// <returns></returns>
        public IList<Message> Get(string stream, int fromEventNumber, int numberOfEvents)
        {
            var eventStreamSlice = _eventStore
                .ReadStreamEventsForwardAsync(stream, fromEventNumber, numberOfEvents, true).Result;
            return eventStreamSlice.Events.Select(e => ConvertEventToMessage(e.Event, stream)).ToList();
        }

        /// <summary>
        /// Awaitable Get the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="outBoxTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}"/>.</returns>
        public Task<Message> GetAsync(
            Guid messageId, 
            int outBoxTimeout = -1, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Returns multiple events from a given stream.
        ///     If all the events do not exist, as many as can be found will be returned.
        /// </summary>
        /// <param name="stream">The stream name.</param>
        /// <param name="fromEventNumber">The event number to start from (inclusive).</param>
        /// <param name="numberOfEvents">The number of events to return.</param>
        /// <returns></returns>
        public async Task<IList<Message>> GetAsync(string stream, int fromEventNumber, int numberOfEvents)
        {
            var eventStreamSlice =
                await _eventStore.ReadStreamEventsForwardAsync(stream, fromEventNumber, numberOfEvents, true);
            return eventStreamSlice.Events.Select(e => ConvertEventToMessage(e.Event, stream)).ToList();
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            //TODO: Implement mark dispatched
            throw new NotImplementedException();
        }
          
        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null)
        {
            //TODO: Implement mark dispatched
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns messages that have yet to be dispatched
        /// </summary>
        /// <param name="millSecondsSinceSent">How long ago as the message sent?</param>
        /// <param name="pageSize">How many messages to return at once?</param>
        /// <param name="pageNumber">Which page number of messages</param>
         /// <param name="args">Additional parameters required for search, if any</param>
        /// <returns>A list of messages that are outstanding for dispatch</returns>
        public IEnumerable<Message> OutstandingMessages(
            double millSecondsSinceSent, 
            int pageSize = 100, 
            int pageNumber = 1,
            Dictionary<string, object> args = null)
        {
            //TODO: Implement outstanding message support
            throw new NotImplementedException();
        }

        private static void AddMetadataToHeader(byte[] metadata, MessageHeader messageHeader, long eventNumber,
            string stream)
        {
            messageHeader.Bag.Add("streamId", stream);
            messageHeader.Bag.Add("eventNumber", eventNumber);

            var metadataJson =
                JsonConvert.DeserializeObject<Dictionary<string, object>>(Encoding.UTF8.GetString(metadata));
            foreach (var entry in metadataJson)
            {
                messageHeader.Bag.Add(entry.Key, entry.Value);
            }
        }

        private static EventData[] CreateEventData(Message message, Dictionary<string, object> headerBag)
        {
            var eventBody = Encoding.UTF8.GetBytes(message.Body.Value);

            var header = IdempotentlyRemoveEventStoreHeaderItems(headerBag);

            var headerBagJson = JsonConvert.SerializeObject(header, new KeyValuePairConverter());
            var eventHeader = Encoding.UTF8.GetBytes(headerBagJson);

            return new[] {new EventData(message.Id, message.Header.Topic, true, eventBody, eventHeader)};
        }

        private static Message ConvertEventToMessage(RecordedEvent @event, string stream)
        {
            var messageBody = new MessageBody(Encoding.UTF8.GetString(@event.Data));
            var messageHeader =
                new MessageHeader(@event.EventId, @event.EventType, MessageType.MT_EVENT, @event.Created);

            AddMetadataToHeader(@event.Metadata, messageHeader, @event.EventNumber, stream);

            return new Message(messageHeader, messageBody);
        }

        private static int ExtractEventNumberFromHeader(Dictionary<string, object> headerBag, Guid messageId)
        {
            object version;
            if (!headerBag.TryGetValue("eventNumber", out version))
                throw new FormatException(
                    $"Message, with MessageId {messageId}, does not have an 'eventNumber' in the message header bag.");
            return int.Parse(version.ToString());
        }

        private string ExtractStreamIdFromHeader(Dictionary<string, object> headerBag, Guid messageId)
        {
            object streamId;
            if (!headerBag.TryGetValue("streamId", out streamId))
                throw new FormatException(
                    $"Message, with MessageId {messageId}, does not have a 'streamId' in the message header bag.");
            return (string)streamId;
        }

        private static Dictionary<string, object> IdempotentlyRemoveEventStoreHeaderItems(Dictionary<string, object> headerBag)
        {
            var headerBagWithoutExtras = new Dictionary<string, object>(headerBag);
            headerBagWithoutExtras.Remove("streamId");
            headerBagWithoutExtras.Remove("eventNumber");
            return headerBagWithoutExtras;
        }

    }
}
