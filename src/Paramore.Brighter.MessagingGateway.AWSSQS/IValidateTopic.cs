namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    internal interface IValidateTopic
    {
        (bool, string TopicArn) Validate(string topic);
    }
}
