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
using System.Threading.Tasks;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.messagestore.eventstore
{
    /// <summary>
    /// Class EventStoreMessageStore.
    /// </summary>
    public class EventStoreMessageStore : IAmAMessageStore<Message>, IAmAMessageStoreViewer<Message>
    {
        private readonly IEventStoreConnection _eventStore;
        private readonly ILog _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventStoreMessageStore"/> class.
        /// </summary>
        /// <param name="eventStore">The active connection to an Event Store instance.</param>
        /// <param name="logger">The logger.</param>
        public EventStoreMessageStore(IEventStoreConnection eventStore, ILog logger)
        {
            _eventStore = eventStore;
            _logger = logger;
        }

        /// <summary>
        /// Adds the specified message. 
        /// The message must have a 'streamId' and an 'eventNumber in the message header bag.
        /// The 'streamId' is the name of the stream to append the message to.
        /// The 'eventNumber' should be one greater than the last event in the stream.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Task.</returns>
        public Task Add(Message message)
        {
            _logger.DebugFormat("Adding message to Event Store Message Store: {0}", JsonConvert.SerializeObject(message));
            var eventBody = Encoding.UTF8.GetBytes(message.Body.Value);

            var headerBag = message.Header.Bag;

            var streamId = ExtractStreamIdFromHeader(headerBag, message.Id);
            var eventNumber = ExtractEventNumberFromHeader(headerBag, message.Id);

            var header = IdempotentlyRemoveEventStoreHeaderItems(headerBag);

            var headerBagJson = JsonConvert.SerializeObject(header, new KeyValuePairConverter());
            var eventHeader = Encoding.UTF8.GetBytes(headerBagJson);

            var eventData = new[] { new EventData(message.Id, message.Header.Topic, true, eventBody, eventHeader) };

            var numberOfPreviousEvent = eventNumber - 1;
            return _eventStore.AppendToStreamAsync(streamId, numberOfPreviousEvent, eventData);
        }

        private string ExtractStreamIdFromHeader(Dictionary<string, object> headerBag, Guid messageId)
        {
            object streamId;
            if (!headerBag.TryGetValue("streamId", out streamId))
                throw new FormatException(String.Format("Message, with MessageId {0}, does not have a 'streamId' in the message header bag.", messageId));
            return (string)streamId;
        }

        private static int ExtractEventNumberFromHeader(Dictionary<string, object> headerBag, Guid messageId)
        {
            object version;
            if (!headerBag.TryGetValue("eventNumber", out version))
                throw new FormatException(String.Format("Message, with MessageId {0}, does not have an 'eventNumber' in the message header bag.", messageId));
            return Int32.Parse(version.ToString());
        }

        private static Dictionary<string, object> IdempotentlyRemoveEventStoreHeaderItems(Dictionary<string, object> headerBag)
        {
            var headerBagWithoutExtras = new Dictionary<string, object>(headerBag);
            headerBagWithoutExtras.Remove("streamId");
            headerBagWithoutExtras.Remove("eventNumber");
            return headerBagWithoutExtras;
        }

        /// <summary>
        /// Gets the specified message by identifier. Currently not implemented.
        /// </summary>
        /// <param name="messageId">The message identifier.</param>
        /// <returns>Task&lt;Message&gt;.</returns>
        public Task<Message> Get(Guid messageId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns all messages in the store. Currently not implemented
        /// </summary>
        /// <param name="pageSize">Number of messages to return in search results (default = 100)</param>
        /// <param name="pageNumber">Page number of results to return (default = 1)</param>
        /// <returns></returns>
        public Task<IList<Message>> Get(int pageSize = 100, int pageNumber = 1)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns multiple events from a given stream. 
        /// If all the events do not exist, as many as can be found will be returned.
        /// </summary>
        /// <param name="stream">The stream name.</param>
        /// <param name="fromEventNumber">The event number to start from (inclusive).</param>
        /// <param name="numberOfEvents">The number of events to return.</param>
        /// <returns></returns>
        public async Task<IList<Message>> Get(string stream, int fromEventNumber, int numberOfEvents)
        {
            var eventStreamSlice = await _eventStore.ReadStreamEventsForwardAsync(stream, fromEventNumber, numberOfEvents, true);
            return eventStreamSlice.Events.Select(e => ConvertEventToMessage(e.Event, stream)).ToList();
        }

        private static Message ConvertEventToMessage(RecordedEvent @event, string stream)
        {
            var messageBody = new MessageBody(Encoding.UTF8.GetString(@event.Data));
            var messageHeader = new MessageHeader(@event.EventId, @event.EventType, MessageType.MT_EVENT, @event.Created);

            AddMetadataToHeader(@event.Metadata, messageHeader, @event.EventNumber, stream);

            return new Message(messageHeader, messageBody);
        }

        private static void AddMetadataToHeader(byte[] metadata, MessageHeader messageHeader, int eventNumber, string stream)
        {
            messageHeader.Bag.Add("streamId", stream);
            messageHeader.Bag.Add("eventNumber", eventNumber);

            var metadataJson = JsonConvert.DeserializeObject<Dictionary<String, Object>>(Encoding.UTF8.GetString(metadata));
            foreach (var entry in metadataJson)
            {
                messageHeader.Bag.Add(entry.Key, entry.Value);
            }
        }
    }
}
