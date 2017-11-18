namespace Paramore.Brighter.MessagingGateway.Redis
{
    public class BrighterMessageFactory
    {
        public Message Create(string redisMessage)
        {
            //TODO: parse the message for header and body content
            //TODO: Deserialize the header content
            //TODO: Set the body value
            return new Message();
        }
    }
}