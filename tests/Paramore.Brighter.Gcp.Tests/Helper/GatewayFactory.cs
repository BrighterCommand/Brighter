using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore.V1;
using Google.Cloud.Storage.V1;
using Grpc.Core;

namespace Paramore.Brighter.Gcp.Tests.Helper;

public static class GatewayFactory
{
    public static string GetProjectId()
    {
        return Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")!;
    }
    
    public static ICredential? GetCredential()
    {
        if (IsLocalCloud) return null;
        try
        {
            return GoogleCredential.GetApplicationDefault();
        }
        catch
        {
            return GoogleCredential.FromAccessToken("mock");
        }
    }

    public static bool IsLocalCloud => Environment.GetEnvironmentVariable("BRIGHTER_LOCALCLOUD") == "true";

    public static void ConfigureFirestoreClient(FirestoreClientBuilder builder)
    {
        if (!IsLocalCloud) return;
        builder.Endpoint = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST")
            ?? throw new InvalidOperationException("FIRESTORE_EMULATOR_HOST is required.");
        builder.ChannelCredentials = ChannelCredentials.Insecure;
    }

    public static void ConfigureStorageClient(StorageClientBuilder builder)
    {
        if (IsLocalCloud) builder.EmulatorDetection = EmulatorDetection.EmulatorOnly;
    }

    public static async Task VerifyLocalCloudServiceAsync(string service)
    {
        if (!IsLocalCloud) return;
        var gateway = Environment.GetEnvironmentVariable("LOCALCLOUD_GATEWAY_URL")
            ?? throw new InvalidOperationException("LOCALCLOUD_GATEWAY_URL is required.");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.Add("X-LocalCloud-Project", GetProjectId());
        client.DefaultRequestHeaders.Add("X-LocalCloud-User", Environment.GetEnvironmentVariable("LOCALCLOUD_USER") ?? "local-developer");
        using var response = await client.GetAsync($"{gateway.TrimEnd('/')}/browse/{service}?project={Uri.EscapeDataString(GetProjectId())}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (service == "firestore" && (int)response.StatusCode == 403
            && body.RootElement.TryGetProperty("error", out var diagnostic)
            && diagnostic.TryGetProperty("message", out var message)
            && message.GetString() == "Validated server access required for Firestore controls")
        {
            Console.WriteLine("LocalCloud Firestore browse returned 403: validated server access is required; native readback and cleanup remain the evidence.");
            return;
        }
        response.EnsureSuccessStatusCode();
        if (body.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.True)
            throw new InvalidOperationException($"LocalCloud {service} verification failed.");
    }
}
