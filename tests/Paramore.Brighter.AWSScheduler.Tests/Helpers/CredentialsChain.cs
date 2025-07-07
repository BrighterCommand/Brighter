using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;

namespace Paramore.Brighter.AWSScheduler.Tests.Helpers
{
    public class CredentialsChain
    {
        public static (AWSCredentials credentials, RegionEndpoint region) GetAwsCredentials()
        {
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
    }
}
