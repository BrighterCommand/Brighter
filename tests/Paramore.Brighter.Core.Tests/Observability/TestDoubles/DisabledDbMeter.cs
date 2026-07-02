using System.Diagnostics;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Core.Tests.Observability.TestDoubles;

/// <summary>
/// A db meter stub that reports itself disabled and records nothing; used when a test only
/// cares about the messaging meter but the processor requires both meters.
/// </summary>
public class DisabledDbMeter : IAmABrighterDbMeter
{
    public void RecordClientOperation(Activity activity) { }

    public bool Enabled => false;
}
