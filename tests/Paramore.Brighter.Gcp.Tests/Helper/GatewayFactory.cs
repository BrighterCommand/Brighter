using System;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.Helper;

public class GatewayFactory
{
    public static GcpMessagingGatewayConnection CreateFactory()
    {
        return new GcpMessagingGatewayConnection
        {
            Credential = GoogleCredential.GetApplicationDefault(),
            ProjectId = Environment.GetEnvironmentVariable("GCP_PROJECT_ID")!,
            PublishConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            },
            SubscribeConfiguration = cfg =>
            {
                cfg.EmulatorDetection = EmulatorDetection.EmulatorOrProduction;
            }
        };
    }
}
