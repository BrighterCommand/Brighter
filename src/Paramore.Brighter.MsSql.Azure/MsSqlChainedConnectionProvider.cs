using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.MsSql.Azure
{
    public class ServiceBusChainedClientProvider : MsSqlAzureConnectionProviderBase
    {
        private readonly ChainedTokenCredential _credential;
        
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using Default Azure Credentials to acquire Access Tokens.
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration</param>
        /// <param name="credentialSources">List of Token Providers to use when trying to obtain a token.</param>
        public ServiceBusChainedClientProvider(RelationalDatabaseConfiguration configuration,
            params TokenCredential[] credentialSources) : base(configuration)
        {
            if (credentialSources == null || credentialSources.Length < 1)
            {
                throw new ArgumentNullException(nameof(credentialSources),
                    "Credential Sources is null or empty, ensure this is set in the constructor.");
            }

            _credential = new ChainedTokenCredential(credentialSources);
        }
        
        protected override AccessToken GetAccessTokenFromProvider()
        {
            return GetAccessTokenFromProviderAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        protected override async Task<AccessToken> GetAccessTokenFromProviderAsync(CancellationToken cancellationToken)
        {
            return await _credential.GetTokenAsync(new TokenRequestContext(_authenticationTokenScopes), cancellationToken);
        }
    }
}
