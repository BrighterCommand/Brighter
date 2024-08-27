namespace Paramore.Brighter.Observability;

/// <summary>
/// What is the operation that the message pump is performing
/// </summary>
public enum MessagePumpSpanOperation
{
    Begin = 0,      // Begin the message pump
    Receive = 1,    // Receive a message via a pull
    Process = 2,    // Process a message from a push
}
