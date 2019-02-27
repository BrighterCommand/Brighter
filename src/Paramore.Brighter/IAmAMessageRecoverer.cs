using System.Collections.Generic;

namespace Paramore.Brighter
{
    /// <summary>
    /// Interface IAmAMessageRecoverer
    /// Used to support reposting a message from a <see cref="IAmAnOutbox{T}"/> to a broker via <see cref="IAmAMessageProducer"/>
    /// </summary>
    public interface IAmAMessageRecoverer
    {
        void Repost(List<string> messageIds, IAmAnOutbox<Message> outBox, IAmAMessageProducer messageProducer);
    }
}