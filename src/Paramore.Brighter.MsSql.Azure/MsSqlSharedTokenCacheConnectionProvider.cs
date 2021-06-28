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
        
        public MsSqlSharedTokenCacheConnectionProvider(MsSqlConfiguration configuration) : base(configuration)
        {
            _azureUserName = Environment.GetEnvironmentVariable(_azureUserNameKey);
            _azureTenantId = Environment.GetEnvironmentVariable(_azureTenantIdKey);
        }
        
        public MsSqlSharedTokenCacheConnectionProvider(MsSqlConfiguration configuration, string userName, string tenantId) : base(configuration)
        {
            _azureUserName = userName;
            _azureTenantId = tenantId;
        }

        protected override AccessToken GetAccessToken()
        {
            return GetAccessTokenAsync(CancellationToken.None).Result;
        }

        protected override async Task<AccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var credential = GetCredential();
            return await credential.GetTokenAsync(new TokenRequestContext(_authenticationTokenScopes), cancellationToken);
        }
        
        private SharedTokenCacheCredential GetCredential()
        {
            return new SharedTokenCacheCredential(new SharedTokenCacheCredentialOptions()
            {
                Username = _azureUserName,
                TenantId = _azureTenantId,
            });
        }
    }
}
