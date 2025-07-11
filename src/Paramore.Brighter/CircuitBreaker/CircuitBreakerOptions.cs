namespace Paramore.Brighter.CircuitBreaker
{

    /// <summary>
    /// The configuration options for <see cref="IAmACircuitBreaker"/>
    /// </summary>
    public class CircuitBreakerOptions
    {
        /// <summary>
        /// Circuit break unhealthy topics for a specific cooldown period in ticks (on clear outbox)
        /// </summary>
        public int CooldownCount { get; set; } = 10;
    }
}
