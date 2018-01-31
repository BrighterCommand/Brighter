namespace Paramore.Brighter.MessagingGateway.MsSql.SqlQueues
{
    /// <summary>
    ///     QueuesMessage object as stored in the Db
    /// </summary>
    public class QueuedMessage
    {
        public QueuedMessage(string jsonContent, string topic, string messageType, long id)
        {
            JsonContent = jsonContent;
            Topic = topic;
            MessageType = messageType;
            Id = id;
        }

        public string JsonContent { get; }
        public string Topic { get; }
        public string MessageType { get; }
        public long Id { get; }

        public static QueuedMessage Empty => new QueuedMessage(string.Empty, string.Empty, string.Empty, 0);
    }
}
