using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public static class HeaderNames
    {
        public static readonly string Id = "id";
        public static string Topic = "topic";
        public static string ContentType = "content-type";
        public static readonly string CorrelationId = "correlation-id";
        public static readonly string HandledCount = "handled-count";
        public static readonly string MessageType = "message-type";
        public static readonly string Timestamp = "timestamp";
        public static readonly string ReplyTo = "reply-to";
        public static string Bag = "bag"; 
    }
}
