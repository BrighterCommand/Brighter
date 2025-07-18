using System;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
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
        return Environment.GetEnvironmentVariable("GCP_PROJECT_ID")!;
    }
    
    public static ICredential GetCredential()
    {
        return GoogleCredential.GetApplicationDefault();
    }
}
