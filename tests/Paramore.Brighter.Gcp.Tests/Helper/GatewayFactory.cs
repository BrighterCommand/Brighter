using System;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.Helper;

public class GatewayFactory
{
    public static GcpMessagingGatewayConnection CreateFactory()
    {
        var gcpGateway = new GcpMessagingGatewayConnection { ProjectId = Environment.GetEnvironmentVariable("GCP_PROJECT_ID") ?? "brighter" };
        return gcpGateway;
    }
}
