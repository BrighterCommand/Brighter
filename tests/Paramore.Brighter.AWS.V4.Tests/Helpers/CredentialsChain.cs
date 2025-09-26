using System;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Paramore.Brighter.Transformers.AWS.V4;

namespace Paramore.Brighter.AWS.V4.Tests.Helpers;

public static class CredentialsChain
{
    public static (AWSCredentials credentials, RegionEndpoint region) GetAwsCredentials()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOCALSTACK_SERVICE_URL")))
        {
            return (new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1);
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

    public static string GetBucketAddressTemple()
    {
        var localStack = Environment.GetEnvironmentVariable("LOCALSTACK_SERVICE_URL");
        if (!string.IsNullOrEmpty(localStack) && Uri.TryCreate(localStack, UriKind.Absolute, out var uri))
        {
            return $"http://{{BucketName}}.s3.{{BucketRegion}}.{uri.Authority}";
        }

        return S3LuggageOptions.DefaultBucketAddressTemplate;
    }
}
