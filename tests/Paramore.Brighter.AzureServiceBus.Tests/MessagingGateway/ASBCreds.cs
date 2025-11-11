using System;
using Paramore.Brighter.MessagingGateway.AzureServiceBus.ClientProvider;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway;

internal static class ASBCreds
{
    public static IServiceBusClientProvider ASBClientProvider
    {
        get
        {
            var connString = Environment.GetEnvironmentVariable("BrighterTestsASBConnectionString");
            var asbNamespace = Environment.GetEnvironmentVariable("BrighterTestsASBNameSpace");
            
            if (!string.IsNullOrEmpty(connString))
            {
                return new ServiceBusConnectionStringClientProvider(connString);
            }
            
            if(!string.IsNullOrEmpty(asbNamespace))
            {
                return new ServiceBusVisualStudioCredentialClientProvider(asbNamespace);
            }
            
            throw new Exception("ASB ConnectionString or Namespace not set not set");
        }
    }
}
