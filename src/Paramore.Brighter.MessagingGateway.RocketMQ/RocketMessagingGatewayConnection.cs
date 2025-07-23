using System;
using Org.Apache.Rocketmq;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.RocketMQ;

/// <summary>
/// Configuration class for RocketMQ messaging gateway in Brighter pipeline.
/// Implements RocketMQ's high-throughput messaging configuration pattern.
/// </summary>
public class RocketMessagingGatewayConnection(ClientConfig config) : IAmGatewayConfiguration
{
    /// <summary>
    /// Gets or sets the time provider for time-sensitive operations.
    /// Used for implementing RocketMQ's message delay and timeout features.
    /// </summary>
    public TimeProvider TimerProvider { get; set; } = TimeProvider.System;
    
    /// <summary>
    /// Gets the RocketMQ client configuration.
    /// Contains core settings like broker addresses and connection pool size.
    /// </summary>
    public ClientConfig ClientConfig { get; } = config;

    /// <summary>
    /// Gets or sets the maximum retry attempts for failed message operations.
    /// Implements RocketMQ's at-least-once delivery guarantee through retries.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the transaction checker for RocketMQ transactional messages.
    /// Handles local transaction state checks during message recovery.
    /// </summary>
    public ITransactionChecker? Checker { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="InsufficientExecutionStackException"/>
    /// </summary>
    public InstrumentationOptions Instrumentation { get; set; } = InstrumentationOptions.All;
}
