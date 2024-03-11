#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter
{
    public class MessageTelemetry
    {
        public const string EventIdHeaderName = "cloudevents.event_id";
        public const string SourceHeaderName = "cloudevents.event_source";
        public const string EventTypeHeaderName = "cloudevents.event_type";
        public const string SubjectHeaderName = "cloudevents.event_subject";

        /// <summary>
        /// The event_id uniquely identifies the event.
        /// </summary>
        /// <example>
        /// 123e4567-e89b-12d3-a456-426614174000; 0001
        /// </example>
        public string EventId { get;}
        
        /// <summary>
        /// The source identifies the context in which an event happened.
        /// </summary>
        /// <example>
        /// https://github.com/cloudevents; /cloudevents/spec/pull/123; my-service
        /// </example>
        public string Source { get;}

        /// <summary>
        /// The event_type contains a value describing the type of event related to the originating occurrence.
        /// </summary>
        /// <example>
        /// com.github.pull_request.opened; com.example.object.deleted.v2
        /// </example>
        public string EventType { get;}
        
        /// <summary>
        /// The subject of the event in the context of the event producer (identified by source).
        /// </summary>
        /// <example>
        /// mynewfile.jpg
        /// </example>
        public string Subject { get;}

        /// <summary>
        /// Telemetry properties
        /// </summary>
        /// <param name="eventId">The event_id uniquely identifies the event.</param>
        /// <param name="source">The source identifies the context in which an event happened.</param>
        /// <param name="eventType">The event_type contains a value describing the type of event related to the originating occurrence.</param>
        /// <param name="subject">The subject of the event in the context of the event producer (identified by source).</param>
        public MessageTelemetry(string eventId, string source, string eventType, string subject = null)
        {
            EventId = eventId;
            Source = source;
            EventType = eventType;
            Subject = subject;
        }
    }
}
