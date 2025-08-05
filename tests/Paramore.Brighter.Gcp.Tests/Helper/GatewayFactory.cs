using System;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.Helper;

public static class GatewayFactory
{
    public static GcpMessagingGatewayConnection CreateFactory()
    {
        return new GcpMessagingGatewayConnection
        {
            Credential = GetCredential(),
            ProjectId = GetProjectId(),
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

    public static string GetProjectId()
    {
        return Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")!;
    }
    
    public static ICredential GetCredential()
    {
        return GoogleCredential.GetApplicationDefault();
    }
}
