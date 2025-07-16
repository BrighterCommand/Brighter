namespace Paramore.Brighter.CircuitBreaker
{

    /// <summary>
    /// The configuration options for <see cref="IAmAnOutboxCircuitBreaker"/>
    /// </summary>
    public class OutboxCircuitBreakerOptions
    {
        /// <value>
        /// Circuit break unhealthy topics for a specific cooldown period in ticks (on clear outbox)
        /// </value>
        public int CooldownCount { get; set; } = 10;
    }
}
