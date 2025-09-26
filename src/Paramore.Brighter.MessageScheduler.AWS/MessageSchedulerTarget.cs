using Paramore.Brighter.Scheduler.Events;

namespace Paramore.Brighter.MessageScheduler.AWS;

/// <summary>
/// The source trigger for fired scheduler message
/// </summary>
public enum MessageSchedulerTarget
{
    /// <summary>
    /// The AWS scheduler will scheduler the <see cref="FireSchedulerMessage"/> on Sqs
    /// </summary>
    /// <remarks>
    /// For this case is necessary to configure a subscription to <see cref="FireSchedulerMessage"/>
    /// </remarks>
    Sqs,

    /// <summary>
    /// The AWS scheduler will scheduler the <see cref="FireSchedulerMessage"/> on SNS
    /// </summary>
    /// <remarks>
    /// For this case is necessary to configure a subscription to <see cref="FireSchedulerMessage"/>
    /// </remarks>
    Sns
}
