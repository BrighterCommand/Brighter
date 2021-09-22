using Paramore.Brighter;

namespace Greetings.Ports.Commands
{
    public class GreetingReply : Reply
    {
        public string Salutation { get; set; }
        
        public GreetingReply() : base(new ReplyAddress())
        {
            
        }
        
        public GreetingReply(ReplyAddress sendersAddress) : base(sendersAddress)
        {
        }
        
    }
}
