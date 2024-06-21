using Paramore.Brighter.ServiceActivator.Control.Events;

namespace Paramore.Brighter.ServiceActivator.Control.Api.Responses;

public class GetStatusResponseSubscriptionInformation
{
    public GetStatusResponseSubscriptionInformation(NodeStatusSubscriptionInformation subscriptionInformation)
    {
        TopicName = subscriptionInformation.TopicName;
        Performers = subscriptionInformation.Performers;
        ActivePerformers = subscriptionInformation.ActivePerformers;
        ExpectedPerformers = subscriptionInformation.ExpectedPerformers;
        IsHealthy = subscriptionInformation.IsHealthy;

    }

    /// <summary>
    /// Name of Topic
    /// </summary>
    public string TopicName { get; }

    /// <summary>
    /// The name of all the Performers
    /// </summary>
    public string[] Performers { get; }

    /// <summary>
    /// Number of currently active performers
    /// </summary>
    public int ActivePerformers { get; }

    /// <summary>
    /// Number of expected performers
    /// </summary>
    public int ExpectedPerformers { get;}

    /// <summary>
    /// Is this subscription healthy on this node
    /// </summary>
    public bool IsHealthy { get; }
}
