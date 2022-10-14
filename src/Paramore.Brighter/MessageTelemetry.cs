using System;

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
