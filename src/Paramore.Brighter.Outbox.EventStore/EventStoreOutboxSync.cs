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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EventStore.ClientAPI;
using Paramore.Brighter.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Paramore.Brighter.Outbox.EventStore
{
    /// <summary>
    ///     Class EventStoreOutbox.
    /// </summary>
    public class EventStoreOutboxSync :
        IAmAnOutboxSync<Message>,
        IAmAnOutboxAsync<Message>
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<EventStoreOutboxSync>();

        private readonly IEventStoreConnection _eventStore;

       /// <summary>
        /// If false we the default thread synchronization context to run any continuation, if true we re-use the original synchronization context.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait 
        /// or access the Result or otherwise block. You may need the orginating synchronization context if you need to access thread specific storage
        /// such as HTTPContext 
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="EventStoreOutboxSync" /> class.
        /// </summary>
        /// <param name="eventStore">The active subscription to an Event Store instance.</param>
        public EventStoreOutboxSync(IEventStoreConnection eventStore)
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
        public void Add(Message message, int outBoxTimeout = -1, IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            s_logger.LogDebug("Adding message to Event Store Outbox: {Request}", JsonSerializer.Serialize(message, JsonSerialisationOptions.Options));

            var headerBag = message.Header.Bag;
            var streamId = ExtractStreamIdFromHeader(headerBag, message.Id);
            var eventNumber = ExtractEventNumberFromHeader(headerBag, message.Id);
            var numberOfPreviousEvent = eventNumber - 1;
            var eventData = EventStoreMessageWriter.CreateEventData(message);

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
            CancellationToken cancellationToken = default(CancellationToken), IAmABoxTransactionConnectionProvider transactionConnectionProvider = null)
        {
            s_logger.LogDebug("Adding message to Event Store Outbox: {Request}", JsonSerializer.Serialize(message, JsonSerialisationOptions.Options));

            var streamId = ExtractStreamIdFromHeader(message.Header.Bag, message.Id);
            var eventNumber = ExtractEventNumberFromHeader(message.Header.Bag, message.Id);
            var numberOfPreviousEvent = eventNumber - 1;
            var eventData = EventStoreMessageWriter.CreateEventData(message);

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
        public IList<Message> Get(
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object> args = null)
        {
            return GetAsync(pageSize, pageNumber, args).Result;
        }

        public async Task<IList<Message>> GetAsync(
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            string stream = GetStreamFromArgs(args);

            var fromEventNumber = pageSize * (pageNumber - 1);

            var eventStreamSlice = await _eventStore.ReadStreamEventsForwardAsync(stream, fromEventNumber, pageSize, true);

            return eventStreamSlice.Events.Select(e => EventStoreMessageReader.ConvertEventToMessage(e.Event, stream)).ToList();
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
            return eventStreamSlice.Events.Select(e => EventStoreMessageReader.ConvertEventToMessage(e.Event, stream)).ToList();
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

        public Task<IEnumerable<Message>> GetAsync(IEnumerable<Guid> messageIds, int outBoxTimeout = -1,
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
            return eventStreamSlice.Events.Select(e => EventStoreMessageReader.ConvertEventToMessage(e.Event, stream)).ToList();
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        /// <param name="args">Additional parameters required for search, if any</param>
        /// <param name="cancellationToken">Allows the sender to cancel the request pipeline. Optional</param>
        public async Task MarkDispatchedAsync(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            var stream = GetStreamFromArgs(args);

            StreamEventsSlice slice;
            var startPos = (long)StreamPosition.End;
            long? nextEventNumber = null;
            RecordedEvent resolvedEvent;
            bool found = false;

            do
            {
                slice = await _eventStore.ReadStreamEventsBackwardAsync(stream, startPos, 100, true);
                startPos = slice.NextEventNumber;

                if (nextEventNumber is null)
                    nextEventNumber = (await _eventStore.ReadStreamEventsBackwardAsync(stream, StreamPosition.End, 1, true)).LastEventNumber;

                resolvedEvent = slice.Events.FirstOrDefault(e => e.Event.EventId == id).Event;

                if (resolvedEvent != null)
                    found = true;
            } while (!found && !slice.IsEndOfStream);

            if (resolvedEvent is null)
                return;

            var message = EventStoreMessageReader.ConvertEventToMessage(resolvedEvent, stream, dispatchedAt, nextEventNumber.Value);

            var eventData = EventStoreMessageWriter.CreateEventData(message);

            await _eventStore.AppendToStreamAsync(stream, nextEventNumber.Value, eventData);
        }

        public Task MarkDispatchedAsync(IEnumerable<Guid> ids, DateTime? dispatchedAt = null, Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<Message>> DispatchedMessagesAsync(double millisecondsDispatchedSince, int pageSize = 100, int pageNumber = 1,
            int outboxTimeout = -1, Dictionary<string, object> args = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Update a message to show it is dispatched
        /// </summary>
        /// <param name="id">The id of the message to update</param>
        /// <param name="dispatchedAt">When was the message dispatched, defaults to UTC now</param>
        public void MarkDispatched(Guid id, DateTime? dispatchedAt = null, Dictionary<string, object> args = null)
        {
            MarkDispatchedAsync(id, dispatchedAt, args).GetAwaiter().GetResult();
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
            return OutstandingMessagesAsync(millSecondsSinceSent, pageSize, pageNumber, args).Result;
        }

        public void Delete(params Guid[] messageIds)
        {
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
        public async Task<IEnumerable<Message>> OutstandingMessagesAsync(
            double millSecondsSinceSent,
            int pageSize = 100,
            int pageNumber = 1,
            Dictionary<string, object> args = null,
            CancellationToken cancellationToken = default)
        {
            var stream = GetStreamFromArgs(args);
            var sentBefore = DateTime.UtcNow.AddMilliseconds(millSecondsSinceSent * -1);

            var fromEventNumber = pageSize * (pageNumber - 1);

            var eventStreamSlice = await _eventStore.ReadStreamEventsBackwardAsync(stream, fromEventNumber, pageSize, true);

            var messages = eventStreamSlice.Events
                .Where(e => e.Event.Created <= sentBefore)
                .Select(e => EventStoreMessageReader.ConvertEventToMessage(e.Event, stream))
                .ToList();

            HashSet<Guid> dispatchedIds = new HashSet<Guid>();
            List<Message> outstandingMessages = new List<Message>();

            foreach (var message in messages)
            {
                var dispatchedAt = message.Header.Bag.TryGetValue(Globals.DispatchedAtKey, out object value)
                    ? value as string
                    : null;

                if (dispatchedAt is null)
                {
                    outstandingMessages.Add(message);
                    continue;
                }

                var previousEventId = message.Header.Bag[Globals.PreviousEventIdKey] as string;

                if (!Guid.TryParse(previousEventId, out Guid eventId))
                    continue;

                if (!dispatchedIds.Contains(eventId))
                {
                    dispatchedIds.Add(eventId);
                    continue;
                }

                outstandingMessages.Add(message);
            }

            return outstandingMessages.Where(om => !dispatchedIds.Contains(om.Id));
        }

        public Task DeleteAsync(CancellationToken cancellationToken, params Guid[] messageIds)
        {
            throw new NotImplementedException();
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

        private static string GetStreamFromArgs(Dictionary<string, object> args)
        {
            if (args is null)
                throw new ArgumentNullException(nameof(args));

            if (!args.ContainsKey(Globals.StreamArg))
                throw new ArgumentException($"{Globals.StreamArg} missing", nameof(args));

            var stream = args[Globals.StreamArg] as string;

            if (string.IsNullOrEmpty(stream))
                throw new ArgumentException($"{Globals.StreamArg} value must not be null or empty", nameof(args));
            return stream;
        }
    }
}
