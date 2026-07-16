using System;
using Xunit;

namespace Paramore.Brighter.MessagingGateway.NATS.Tests;

/// <summary>
/// Skips the test unless the <code>NATS_URL</code> environment variable is set.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class NatsAvailableFactAttribute : FactAttribute
{
    public NatsAvailableFactAttribute()
    {
        var url = Environment.GetEnvironmentVariable("NATS_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            Skip = "NATS_URL environment variable is not set; skipping NATS integration test.";
        }
    }
}
