using System.Diagnostics;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

/// <summary>
/// Spy that counts how many times the processor asks us to record a client operation.
/// Enabled is true so the processor does not short-circuit on the meter being disabled.
/// </summary>
public class SpyMessagingMeter : IAmABrighterMessagingMeter
{
    public int RecordClientOperationCallCount { get; private set; }

    public void RecordClientOperation(Activity activity) => RecordClientOperationCallCount++;

    public void AddClientSentMessage(Activity activity) { }

    public void AddClientConsumedMessage(Activity activity) { }

    public void RecordProcess(Activity activity) { }

    public bool Enabled => true;
}
