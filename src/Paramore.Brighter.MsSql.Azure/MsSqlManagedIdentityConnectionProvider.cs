using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.MsSql.Azure
{
    public class MsSqlManagedIdentityConnectionProvider : MsSqlAzureConnectionProviderBase
    {
        public MsSqlManagedIdentityConnectionProvider(MsSqlConfiguration configuration) : base(configuration)
        {
        }

        protected override AccessToken GetAccessToken()
        {
            return GetAccessTokenAsync(CancellationToken.None).Result;
        }

        protected override async Task<AccessToken> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            var credential = new ManagedIdentityCredential();
            return await credential.GetTokenAsync(new TokenRequestContext(_authenticationTokenScopes), cancellationToken);
        }
    }
}
