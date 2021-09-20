namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    internal class SqsMessageCreatorFactory
    {
        public static ISqsMessageCreator Create(bool rawMessageDelivery)
        {
            if (rawMessageDelivery)
            {
                return new SqsMessageCreator();
            }

            return new SqsInlineMessageCreator();
        }
    }
}
