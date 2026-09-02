using System;
using System.Linq;
using Paramore.Brighter.JsonConverters;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    internal static class ASBConstants
    {
        public const string LockTokenHeaderBagKey = "LockToken";
        public const string SequenceNumberBagKey = "SequenceNumber";
        public const string MessageTypeHeaderBagKey = "MessageType";
        public const string HandledCountHeaderBagKey = "HandledCount";
        public const string ReplyToHeaderBagKey = "ReplyTo";
        public const string SessionIdKey = "SessionId";
        public const string CloudEventDataRef = "cloudEvents:dataref";
        public const string CloudEventsSchema = "cloudEvents:schema";
        public const string CloudEventsSource = "cloudEvents:source";
        public const string CloudEventsType = "cloudEvents:type";
        public const string CloudEventsId = "cloudEvents:id";
        public const string CloudEventsSpecVersion = "cloudEvents:specversion";
        public const string CloudEventsContentType = "cloudEvents:contenttype";
        public const string CloudEventsSubject = "cloudEvents:subject";
        public const string CloudEventsTime = "cloudEvents:time";
        public const string OtelTraceParent = "cloudevents:traceparent";
        public const string OtelTraceState = "cloudevents:tracestate";
        public const string CloudEventsParitionKey = "cloudEvents:partitionkey";
        public const string Baggage = "cloudevents:baggage";
        public const string TraceParent = "cloudevents:traceparent";
        public const string TraceState = "cloudevents:tracestate";

        public static readonly string[] ReservedHeaders = [LockTokenHeaderBagKey, SequenceNumberBagKey, MessageTypeHeaderBagKey, HandledCountHeaderBagKey, ReplyToHeaderBagKey, SessionIdKey
        ];

        /// <summary>
        /// Does a header bag <paramref name="key"/> refer to the reserved key <paramref name="reservedKey"/>?
        /// Brighter's Outbox serializes the bag with <see cref="JsonSerialisationOptions.Options"/>, whose
        /// <see cref="System.Text.Json.JsonSerializerOptions.PropertyNamingPolicy"/> rewrites bag keys — so a key
        /// written as "SessionId" comes back transformed (for example "sessionId" or "session_id") depending on the
        /// configured policy. We therefore match both the raw key and the key as the active naming policy would
        /// render it, case-insensitively, so the lookup stays correct whatever policy the user configures.
        /// </summary>
        public static bool IsBagKey(string key, string reservedKey)
        {
            if (string.Equals(key, reservedKey, StringComparison.OrdinalIgnoreCase))
                return true;

            var namingPolicy = JsonSerialisationOptions.Options.PropertyNamingPolicy;
            return namingPolicy is not null
                   && string.Equals(key, namingPolicy.ConvertName(reservedKey), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Is a header bag <paramref name="key"/> one of the <see cref="ReservedHeaders"/>, accounting for any
        /// naming-policy transformation applied during an Outbox round-trip? See <see cref="IsBagKey"/>.
        /// </summary>
        public static bool IsReservedHeader(string key) => ReservedHeaders.Any(reserved => IsBagKey(key, reserved));
    }
}
