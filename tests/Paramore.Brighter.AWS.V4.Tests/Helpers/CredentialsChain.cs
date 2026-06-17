using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Paramore.Brighter.Transformers.AWS.V4;

namespace Paramore.Brighter.AWS.V4.Tests.Helpers;

public static class CredentialsChain
{
    // Per-process random 12-digit account ID; Floci treats a 12-digit access key
    // as the AWS account ID for per-account resource isolation, so each test-runner
    // process sees its own namespace and can share a single Floci container with
    // other processes (e.g. V3 vs V4) without name collisions.
    private static readonly string s_localAccountId =
        Random.Shared.NextInt64(100_000_000_000L, 999_999_999_999L + 1).ToString();

    public static (AWSCredentials credentials, RegionEndpoint region) GetAwsCredentials()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_SERVICE_URL")))
        {
            return (new BasicAWSCredentials(s_localAccountId, "test"), RegionEndpoint.USEast1);
        }
            
        //Try to get the details out of the credential store chain
        var credentialChain = new CredentialProfileStoreChain();
        if (credentialChain.TryGetAWSCredentials("default", out var credentials) &&
            credentialChain.TryGetProfile("default", out var profile))
        {
            return (credentials, profile.Region);
        }
            
        //if not, can we grab them from environment variables - will throw
        return (new EnvironmentVariablesAWSCredentials(), new EnvironmentVariableAWSRegion().Region);
    }

    public static string GetBucketAddressTemplate()
    {
        var serviceUrl = Environment.GetEnvironmentVariable("AWS_SERVICE_URL");
        if (!string.IsNullOrEmpty(serviceUrl) && Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri))
        {
            return $"http://{{BucketName}}.s3.{{BucketRegion}}.{uri.Authority}";
        }

        return S3LuggageOptions.DefaultBucketAddressTemplate;
    }
}
