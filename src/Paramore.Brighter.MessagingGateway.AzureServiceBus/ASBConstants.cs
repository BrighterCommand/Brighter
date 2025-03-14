namespace Paramore.Brighter.MessagingGateway.AzureServiceBus
{
    internal static class ASBConstants
    {
        public const string LockTokenHeaderBagKey = "LockToken";
        public const string MessageTypeHeaderBagKey = "MessageType";
        public const string HandledCountHeaderBagKey = "HandledCount";
        public const string ReplyToHeaderBagKey = "ReplyTo";
        public const string SessionIdKey = "SessionId";

        public static readonly string[] ReservedHeaders =
            new[] {LockTokenHeaderBagKey, MessageTypeHeaderBagKey, HandledCountHeaderBagKey, ReplyToHeaderBagKey, SessionIdKey };
    }
}
