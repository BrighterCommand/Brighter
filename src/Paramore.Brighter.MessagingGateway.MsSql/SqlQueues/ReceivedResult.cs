namespace Paramore.Brighter.MessagingGateway.MsSql.SqlQueues
{
    public class ReceivedResult : QueuedMessage
    {
        /// <summary>
        ///     Typeless received result
        /// </summary>
        /// <param name="isDataValid">True iff data was received from the queue</param>
        /// <param name="jsonContent">The json serialized representation of the message</param>
        /// <param name="topic">The topic name</param>
        /// <param name="messageType">The type of the serialized message</param>
        /// <param name="id">Database Id</param>
        public ReceivedResult(bool isDataValid, string jsonContent, string topic, string messageType, long id)
            : base(jsonContent, topic, messageType, id)
        {
            IsDataValid = isDataValid;
        }

        public bool IsDataValid { get; }

        public new static ReceivedResult Empty => new ReceivedResult(false, string.Empty, string.Empty, string.Empty, 0);
    }

    /// <summary>
    ///     Typed received result
    /// </summary>
    /// <typeparam name="T">The type of the message</typeparam>
    public class ReceivedResult<T> : ReceivedResult
    {
        public ReceivedResult(bool isDataValid, string jsonContent, string topic, string messageType, long id, T message)
            : base(isDataValid, jsonContent, topic, messageType, id)
        {
            Message = message;
        }

        public T Message { get; }

        public new static ReceivedResult<T> Empty => new ReceivedResult<T>(false, string.Empty, string.Empty, string.Empty, 0, default(T));
    }
}
