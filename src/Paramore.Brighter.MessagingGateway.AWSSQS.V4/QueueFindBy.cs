namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// How do we validate the queue - when we opt to validate or create (already exists) infrastructure
/// Relates to how we interpret the RoutingKey. Is it an Arn (0 or 1) or a name (2)
/// 0 - The queue is supplied as an url, and should be checked with a GetQueueAttributes call. May be any account.
/// 2 - The topic is supplies as a name, and should be checked by a GetQueueUrlAsync call. Must be in caller's account.
/// </summary>
public enum QueueFindBy
{
    Url,
    Name
}
