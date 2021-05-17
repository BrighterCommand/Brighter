﻿#region Licence

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

# endregion

using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using EventStore.ClientAPI;

namespace Paramore.Brighter.Outbox.EventStore
{
    internal class EventStoreMessageWriter
    {
        public static EventData[] CreateEventData(Message message)
        {
            var eventBody = Encoding.UTF8.GetBytes(message.Body.Value);

            var headerCopy = message.Header.Copy();
            RemoveEventStoreHeaderItems(headerCopy.Bag);

            var headerJson = JsonSerializer.Serialize(headerCopy, JsonSerialisationOptions.Options);
            var eventHeader = Encoding.UTF8.GetBytes(headerJson);

            return new[] {new EventData(message.Id, message.Header.Topic, true, eventBody, eventHeader)};
        } 
        
        private static void RemoveEventStoreHeaderItems(Dictionary<string, object> headerBag)
        {
            headerBag.Remove("streamId");
            headerBag.Remove("eventNumber");
        }
    }
}
