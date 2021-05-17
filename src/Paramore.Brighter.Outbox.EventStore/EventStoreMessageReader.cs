#region Licence

/* The MIT License (MIT)
Copyright © 2021 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Text.Json;
using EventStore.ClientAPI;

namespace Paramore.Brighter.Outbox.EventStore
{
    /// <summary>
    /// Helps serialize an object to and from
    /// </summary>
    internal class EventStoreMessageReader
    {
        public static Message ConvertEventToMessage(RecordedEvent @event, string stream, DateTime? dispatchedAt = null, long? eventNumber = null)
        {
            var messageBody = new MessageBody(Encoding.UTF8.GetString(@event.Data));

            eventNumber = eventNumber.HasValue ? eventNumber : @event.EventNumber;

            var messageHeader = CreateHeader(@event.Metadata, eventNumber.Value, stream, dispatchedAt);

            return new Message(messageHeader, messageBody);
        }
        
        private static MessageHeader CreateHeader(byte[] metadata, long eventNumber, string stream, DateTime? dispatchedDate = null, Guid? previousEventId = null)
        {
            MessageHeader messageHeader = DeserializeMessageHeader(metadata);

            ResetEventStoreProperties(eventNumber, stream, dispatchedDate, previousEventId, messageHeader);

            return messageHeader;
        }

        private static MessageHeader DeserializeMessageHeader(byte[] metadata)
        {
            var json = Encoding.UTF8.GetString(metadata);
            var messageHeader = JsonSerializer.Deserialize<MessageHeader>(json, JsonSerialisationOptions.Options);
            
            FixBagElements(messageHeader);
            
            return messageHeader;
        }

        private static void FixBagElements(MessageHeader messageHeader)
        {
            //fix the bag elements; System.Json will read them as a JsonElement not an object, so we have to guess for it
            var bagEntries = new Dictionary<string, object>();
            foreach (var entry in messageHeader.Bag)
            {
                if (entry.Value != null)
                {
                     var value = (JsonElement)entry.Value;
                     bagEntries.Add(entry.Key, ConvertJsonElementToObject(value));
                }
                else
                {
                    bagEntries.Add(entry.Key, null);
                }
            }

            messageHeader.Bag = bagEntries;
        }

        private static object ConvertJsonElementToObject(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (value.TryGetInt32(out var integer))
                        return integer;
                    return value.GetDecimal();
                case JsonValueKind.String:
                    if (value.TryGetDateTime(out var date))
                    {
                        return date; 
                    }
                    return value.GetString();
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.Object:
                    return value.EnumerateObject().ToDictionary(property => ConvertJsonElementToObject(property.Value)); 
                case JsonValueKind.Null:
                    return null;
                case JsonValueKind.Array:
                    return value.EnumerateArray().Select(element => ConvertJsonElementToObject(element)).ToArray();
                default:
                    throw new ConfigurationException("Bag contains an unsupported type");
            }
        }

        private static void ResetEventStoreProperties(long eventNumber, string stream, DateTime? dispatchedDate, Guid? previousEventId, MessageHeader messageHeader)
        {
            messageHeader.Bag.Add("streamId", stream);
            messageHeader.Bag.Add("eventNumber", eventNumber);

            if (dispatchedDate != null)
            {
                messageHeader.Bag.Add(Globals.DispatchedAtKey, dispatchedDate);
                messageHeader.Bag.Add(Globals.PreviousEventIdKey, previousEventId);
            }
        }
    }
}
