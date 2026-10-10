using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

// The sweeper starvation tests cap the thread pool with ThreadPool.SetMaxThreads and park every
// worker on a gate. Both are process-wide: run alongside another class, they would starve it too
// (and its pool work would unblock ours). Serialising through this collection removes the overlap.
[CollectionDefinition(Name, DisableParallelization = true)]
public class ThreadPoolStarvationCollection
{
    public const string Name = "ThreadPoolStarvation";
}
