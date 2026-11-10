using System.Collections.Generic;
using Paramore.Brighter.CircuitBreaker;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;

public sealed record HeldRead(IReadOnlyList<TopicTrip> Live, IReadOnlyList<TopicTrip> Ended);
