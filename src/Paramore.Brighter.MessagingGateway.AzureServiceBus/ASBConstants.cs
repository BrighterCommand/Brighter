namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    internal static class ASBConstants
    {
        public const string LockTokenHeaderBagKey = "LockToken";
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

        public static readonly string[] ReservedHeaders = [LockTokenHeaderBagKey, MessageTypeHeaderBagKey, HandledCountHeaderBagKey, ReplyToHeaderBagKey, SessionIdKey
        ];
    }
}
