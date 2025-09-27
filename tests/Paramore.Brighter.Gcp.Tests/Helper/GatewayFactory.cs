using System;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Paramore.Brighter.MessagingGateway.GcpPubSub;

namespace Paramore.Brighter.Gcp.Tests.Helper;

public static class GatewayFactory
{
    private static readonly GcpMessagingGatewayConnection s_connection = new()
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
    
    public static GcpMessagingGatewayConnection CreateFactory() => s_connection;

    public static string GetProjectId()
    {
        return Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")!;
    }
    
    public static ICredential? GetCredential()
    {
        try
        {
            return GoogleCredential.GetApplicationDefault();
        }
        catch
        {
            return GoogleCredential.FromAccessToken("mock");
        }
    }
}
