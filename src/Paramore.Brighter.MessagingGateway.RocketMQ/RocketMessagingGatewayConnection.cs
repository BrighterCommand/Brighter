using System;
using Org.Apache.Rocketmq;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// The RocketMQ configuration
/// </summary>
public class RocketMessagingGatewayConnection(ClientConfig config) : IAmGatewayConfiguration
{
    /// <summary>
    /// The <see cref="System.TimeProvider"/>
    /// </summary>
    public TimeProvider TimerProvider { get; set; } = TimeProvider.System;
    
    /// <summary>
    /// The <see cref="ClientConfig" />
    /// </summary>
    public ClientConfig ClientConfig { get; } = config;

    /// <summary>
    /// The max attempt during publish a message
    /// </summary>
    /// <remarks>
    /// Max attempts must be positive
    /// </remarks>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// The  <see cref="ITransactionChecker" />
    /// </summary>
    public ITransactionChecker? Checker { get; set; }
}
