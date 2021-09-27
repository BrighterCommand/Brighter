using System;

namespace Paramore.Brighter.AzureServiceBus.Tests.MessagingGateway
{
    internal static class ASBCreds
    {
        public static string ASBConnectionString { get
            {
                var connString = Environment.GetEnvironmentVariable("BrighterTestsASBConnectionString");
                if (string.IsNullOrEmpty(connString))
                    throw new Exception("ASB ConnectionString not set");
                return connString;
            }
        }
    }
}
