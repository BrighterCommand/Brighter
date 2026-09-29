using System;
using TUnit.Core;
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<Paramore.Brighter.RMQ.Sync.Tests.BrokerParallelLimit>]

namespace Paramore.Brighter.RMQ.Sync.Tests;

public class BrokerParallelLimit : IParallelLimit
{
    // Blocking consumers must leave thread-pool capacity for broker delivery callbacks.
    public int Limit => Math.Min(Environment.ProcessorCount, 4);
}
