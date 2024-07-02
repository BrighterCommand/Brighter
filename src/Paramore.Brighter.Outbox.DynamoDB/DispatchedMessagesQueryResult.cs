using System.Collections.Generic;

namespace Paramore.Brighter.Outbox.DynamoDB
{
    internal class DispatchedMessagesQueryResult
    {
        public IEnumerable<MessageItem> Messages { get; private set; }
        public string PaginationToken { get; private set; }
        public bool QueryComplete { get; private set; }

        public DispatchedMessagesQueryResult(IEnumerable<MessageItem> messages, string paginationToken, bool queryComplete)
        {
            Messages = messages;
            PaginationToken = paginationToken;
            QueryComplete = queryComplete;
        }
    }
}
