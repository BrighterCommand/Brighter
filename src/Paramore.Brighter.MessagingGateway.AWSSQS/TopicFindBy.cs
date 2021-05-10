namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    /// <summary>
    /// How do we validate the topic - when we opt to validate or create (already exists) infrastructure
    /// Relates to how we interpret the RoutingKey. Is it an Arn (0 or 1) or a name (2)
    /// 0 - The topic is supplied as an Arn, and should be checked with a GetTopicAttributes call. May be any account.
    /// 1 - The topic is supplied as a name, and should be turned into an Arn and checked via a GetTopicAttributes call. Must be in caller's account.
    /// 2 - The topic is supplies as a name, and should be checked by a ListTopics call. Must be in caller's account. Rate limited at 30 calls per second
    /// </summary>
    public enum TopicFindBy
    {
        Arn,
        Convention,
        Name,
    }
}
