using System.Threading;

namespace Paramore.Brighter.Tasks;

/// <summary>
/// Represents a context message containing a callback and state.
/// </summary>
public struct ContextMessage
{
    public readonly SendOrPostCallback Callback;
    public readonly ExecutionContext? Context;
    public readonly object? State;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContextMessage"/> struct.
    /// </summary>
    /// <param name="callback">The callback to execute.</param>
    /// <param name="state">The state to pass to the callback.</param>
    public ContextMessage(SendOrPostCallback callback, object? state)
    {
        Callback = callback;
        State = state;
    }
}
