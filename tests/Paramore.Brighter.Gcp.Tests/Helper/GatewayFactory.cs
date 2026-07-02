using System;
using Google.Apis.Auth.OAuth2;

namespace Paramore.Brighter.Gcp.Tests.Helper;

public static class GatewayFactory
{
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
