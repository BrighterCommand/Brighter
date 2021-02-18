using System;

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// When we validated the existence of a topic, it was not found
    /// </summary>
    public class TopicMissingException : Exception
    {
        /// <summary>
        /// No additional data, but the topic was not found
        /// </summary>
        public TopicMissingException() { }
        
        /// <summary>
        /// The topic was not found with additional information
        /// </summary>
        /// <param name="message">What were we trying to do when this happened</param>
        public TopicMissingException(string message) : base(message) { }

        /// <summary>
        /// Another exception prevented us from finding the topic
        /// </summary>
        /// <param name="message">What were we doing when this happened?</param>
        /// <param name="innerException">What was the inner exception</param>
        public TopicMissingException(string message, Exception innerException) : base(message, innerException) { }
    }
}
