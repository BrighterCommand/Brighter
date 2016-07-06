// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor.messagestore.eventstore
// Author           : george
// Created          : 06-11-2015
//
// Last Modified By : george
// Last Modified On : 06-11-2015
// ***********************************************************************
// <copyright file="EventStoreMessageStore.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messagestore.eventstore
{
    /// <summary>
    ///     Class EventStoreMessageStore.
    /// </summary>
    public class EventStoreMessageStore : IAmAMessageStore<Message>, IAmAMessageStoreAsync<Message>
    {
        private readonly IEventStoreConnection _eventStore;
        private readonly ILog _logger;

        /// <summary>
        ///     Initializes a new instance of the <see cref="EventStoreMessageStore" /> class.
        /// </summary>
        /// <param name="eventStore">The active connection to an Event Store instance.</param>
        public EventStoreMessageStore(IEventStoreConnection eventStore)
            : this(eventStore, LogProvider.For<EventStoreMessageStore>()) {}

        /// <summary>
        ///     Initializes a new instance of the <see cref="EventStoreMessageStore" /> class.
        ///     Use this constructor if you need to inject the logger, for example for testing
        /// </summary>
        /// <param name="eventStore">The active connection to an Event Store instance.</param>
        /// <param name="logger">The logger.</param>
        public EventStoreMessageStore(IEventStoreConnection eventStore, ILog logger)
        {
            _eventStore = eventStore;
            _logger = logger;
        }

        /// <summary>
        ///     Adds the specified message.
        ///     The message must have a 'streamId' and an 'eventNumber' in the message header bag.
        ///     The 'streamId' is the name of the stream to append the message to.
        ///     The 'eventNumber' should be one greater than the last event in the stream.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public void Add(Message message, int messageStoreTimeout = -1)
        {
            _logger.DebugFormat("Adding message to Event Store Message Store: {0}", JsonConvert.SerializeObject(message));
            var headerBag = message.Header.Bag;
            var streamId = ExtractStreamIdFromHeader(headerBag, message.Id);
            var eventNumber = ExtractEventNumberFromHeader(headerBag, message.Id);
            var numberOfPreviousEvent = eventNumber - 1;
            var eventData = CreateEventData(message, headerBag);

            _eventStore.AppendToStreamAsync(streamId, numberOfPreviousEvent, eventData).Wait();
        }

        private static EventData[] CreateEventData(Message message, Dictionary<string, object> headerBag)
        {
            var eventBody = Encoding.UTF8.GetBytes(message.Body.Value);

            var header = IdempotentlyRemoveEventStoreHeaderItems(headerBag);

            var headerBagJson = JsonConvert.SerializeObject(header, new KeyValuePairConverter());
            var eventHeader = Encoding.UTF8.GetBytes(headerBagJson);

            var eventData = new[] {new EventData(message.Id, message.Header.Topic, true, eventBody, eventHeader)};
            return eventData;
        }

        /// <summary>
        ///     Gets the specified message by identifier. Currently not implemented.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public Message Get(Guid messageId, int messageStoreTimeout = -1)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Awaitable add the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="messageStoreTimeout">The time allowed for the write in milliseconds; on a -1 default</param>
        /// <param name="ct">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task"/>.</returns>
        public async Task AddAsync(Message message, int messageStoreTimeout = -1, CancellationToken? ct = null)
        {
            _logger.DebugFormat("Adding message to Event Store Message Store: {0}", JsonConvert.SerializeObject(message));
            var headerBag = message.Header.Bag;
            var streamId = ExtractStreamIdFromHeader(headerBag, message.Id);
            var eventNumber = ExtractEventNumberFromHeader(headerBag, message.Id);
            var numberOfPreviousEvent = eventNumber - 1;
            var eventData = CreateEventData(message, headerBag);

            await _eventStore.AppendToStreamAsync(streamId, numberOfPreviousEvent, eventData);
        }

        /// <summary>
        /// If false we the default thread synchronization context to run any continuation, if true we re-use the original synchronization context.
        /// Default to false unless you know that you need true, as you risk deadlocks with the originating thread if you Wait 
        /// or access the Result or otherwise block. You may need the orginating synchronization context if you need to access thread specific storage
        /// such as HTTPContext 
        /// </summary>
        public bool ContinueOnCapturedContext { get; set; }

        /// <summary>
        /// Awaitable Get the specified message identifier.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <param name="messageStoreTimeout">The time allowed for the read in milliseconds; on  a -2 default</param>
        /// <param name="ct">Allows the sender to cancel the request pipeline. Optional</param>
        /// <returns><see cref="Task{Message}"/>.</returns>
        public Task<Message> GetAsync(Guid messageId, int messageStoreTimeout = -1, CancellationToken? ct = null)
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
            var eventStreamSlice =
                _eventStore.ReadStreamEventsForwardAsync(stream, fromEventNumber, numberOfEvents, true).Result;
            return eventStreamSlice.Events.Select(e => ConvertEventToMessage(e.Event, stream)).ToList();
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
            var eventStreamSlice = await _eventStore.ReadStreamEventsForwardAsync(stream, fromEventNumber, numberOfEvents, true);
            return eventStreamSlice.Events.Select(e => ConvertEventToMessage(e.Event, stream)).ToList();
        }

        private static void AddMetadataToHeader(byte[] metadata, MessageHeader messageHeader, int eventNumber,
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

        private static Message ConvertEventToMessage(RecordedEvent @event, string stream)
        {
            var messageBody = new MessageBody(Encoding.UTF8.GetString(@event.Data));
            var messageHeader = new MessageHeader(@event.EventId, @event.EventType, MessageType.MT_EVENT, @event.Created);

            AddMetadataToHeader(@event.Metadata, messageHeader, @event.EventNumber, stream);

            return new Message(messageHeader, messageBody);
        }

        private static int ExtractEventNumberFromHeader(Dictionary<string, object> headerBag, Guid messageId)
        {
            object version;
            if (!headerBag.TryGetValue("eventNumber", out version))
                throw new FormatException(
                    string.Format(
                        "Message, with MessageId {0}, does not have an 'eventNumber' in the message header bag.",
                        messageId));
            return int.Parse(version.ToString());
        }

        private string ExtractStreamIdFromHeader(Dictionary<string, object> headerBag, Guid messageId)
        {
            object streamId;
            if (!headerBag.TryGetValue("streamId", out streamId))
                throw new FormatException(
                    string.Format("Message, with MessageId {0}, does not have a 'streamId' in the message header bag.",
                        messageId));
            return (string) streamId;
        }

        private static Dictionary<string, object> IdempotentlyRemoveEventStoreHeaderItems(
            Dictionary<string, object> headerBag)
        {
            var headerBagWithoutExtras = new Dictionary<string, object>(headerBag);
            headerBagWithoutExtras.Remove("streamId");
            headerBagWithoutExtras.Remove("eventNumber");
            return headerBagWithoutExtras;
        }
    }
}