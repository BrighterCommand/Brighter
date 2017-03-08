using System;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class Reply.
    /// Used for a Command that is a Reply in a Request-Reply exchange. Brighter supports publish-subscribe as its main approach to producers and consumers, but it is possible 
    /// to support request-reply semantics as well. The key is that the sender must include a <see cref="ReplyAddress"/> in the <see cref="Request"/> (the <see cref="IAmAMessageMapper"/>
    /// then populates that into the <see cref="MessageHeader"/> as the replyTo address). When we create a <see cref="Reply"/> then we set the <see cref="ReplyAddress"/> from the <see cref="Request"/>
    /// onto the <see cref="Reply"/> and the <see cref="IAmAMessageMapper"/> for the <see cref="Reply"/> sets this as the topic so that it is routed correctly.
    /// </summary>
    public class Reply : Command
    {
        public ReplyAddress SendersAddress { get; private set; }

        public Reply(ReplyAddress sendersAddress)
            : base(Guid.NewGuid())
        {
            SendersAddress = sendersAddress;
        }
    }
}