using System;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Transformers.AWS;

namespace Paramore.Brighter.AWS.Tests.Helpers;

public static class GatewayFactory
{
    public static AWSMessagingGatewayConnection CreateFactory()
    {
        var (credentials, region) = CredentialsChain.GetAwsCredentials();
        return CreateFactory(credentials, region, config =>
        {
            
        });
    }

    public static AWSMessagingGatewayConnection CreateFactory(Action<ClientConfig> clientConfig)
    {
        var (credentials, region) = CredentialsChain.GetAwsCredentials();
        return CreateFactory(credentials, region, clientConfig);
    }

    public static AWSMessagingGatewayConnection CreateFactory(
        AWSCredentials credentials,
        RegionEndpoint region,
        Action<ClientConfig>? config = null)
    {
        return new AWSMessagingGatewayConnection(credentials, region,
            cfg =>
            {
                config?.Invoke(cfg);

                var serviceUrl = Environment.GetEnvironmentVariable("LOCALSTACK_SERVICE_URL");
                if (!string.IsNullOrWhiteSpace(serviceUrl))
                {
                    if (cfg is AmazonS3Config && Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri))
                    {
                        cfg.ServiceURL = $"http://s3.{uri.Authority}";
                    }
                    else
                    {
                        cfg.ServiceURL = serviceUrl;
                    }
                }
            });
    }

    public static AWSS3Connection CreateS3Connection()
    {
        var (credentials, region) = CredentialsChain.GetAwsCredentials();
        return new AWSS3Connection(credentials, region, cfg =>
        {
            var serviceUrl = Environment.GetEnvironmentVariable("LOCALSTACK_SERVICE_URL");
            if (!string.IsNullOrWhiteSpace(serviceUrl))
            {
                if (cfg is AmazonS3Config && Uri.TryCreate(serviceUrl, UriKind.Absolute, out var uri))
                {
                    cfg.ServiceURL = $"http://s3.{uri.Authority}";
                }
                else
                {
                    cfg.ServiceURL = serviceUrl;
                }
            }
        });
    }
}
