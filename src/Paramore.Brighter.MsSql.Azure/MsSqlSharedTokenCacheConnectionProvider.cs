using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.MsSql.Azure
{
    public class MsSqlSharedTokenCacheConnectionProvider : MsSqlAzureConnectionProviderBase
    {
        private const string _azureUserNameKey = "AZURE_USERNAME";
        private const string _azureTenantIdKey = "AZURE_TENANT_ID";
        
        private readonly string _azureUserName;
        private readonly string _azureTenantId;
        
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using Shared Token Cache Credentials to acquire Access Tokens.
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration</param>
        public MsSqlSharedTokenCacheConnectionProvider(RelationalDatabaseConfiguration configuration) : base(configuration)
        {
            _azureUserName = Environment.GetEnvironmentVariable(_azureUserNameKey);
            _azureTenantId = Environment.GetEnvironmentVariable(_azureTenantIdKey);
        }
        
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using Shared Token Cache Credentials to acquire Access Tokens.
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration</param>
        public MsSqlSharedTokenCacheConnectionProvider(RelationalDatabaseConfiguration configuration, string userName, string tenantId) : base(configuration)
        {
            _azureUserName = userName;
            _azureTenantId = tenantId;
        }

        protected override AccessToken GetAccessTokenFromProvider()
        {
            return GetAccessTokenFromProviderAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        protected override async Task<AccessToken> GetAccessTokenFromProviderAsync(CancellationToken cancellationToken)
        {
            var credential = GetCredential();
            return await credential.GetTokenAsync(new TokenRequestContext(AuthenticationTokenScopes), cancellationToken);
        }
        
        private SharedTokenCacheCredential GetCredential()
        {
            return new SharedTokenCacheCredential(new SharedTokenCacheCredentialOptions
            {
                Username = _azureUserName,
                TenantId = _azureTenantId,
            });
        }
    }
}
