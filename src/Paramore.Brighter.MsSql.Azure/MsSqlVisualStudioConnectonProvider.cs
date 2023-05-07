using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.MsSql.Azure
{
    public class MsSqlVisualStudioConnectonProvider : MsSqlAzureConnectonProviderBase
    {
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using Visual Studio Credentials to acquire Access Tokens.
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration</param>
        public MsSqlVisualStudioConnectonProvider(RelationalDatabaseConfiguration configuration) : base(configuration)
        {
        }

        protected override AccessToken GetAccessTokenFromProvider()
        {
            return GetAccessTokenFromProviderAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        protected override async Task<AccessToken> GetAccessTokenFromProviderAsync(CancellationToken cancellationToken)
        {
            var credential = new VisualStudioCredential();
            return await credential.GetTokenAsync(new TokenRequestContext(_authenticationTokenScopes), cancellationToken);
        }
    }
}
