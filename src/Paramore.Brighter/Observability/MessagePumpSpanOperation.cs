namespace Paramore.Brighter.Observability;

/// <summary>
/// What is the operation that the message pump is performing
/// </summary>
public enum MessagePumpSpanOperation
{
    Receive = 0,    // Receive a message via a pull
    Process = 1,    // Process a message from a push
}
