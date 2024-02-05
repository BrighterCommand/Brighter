using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.MsSql.Azure
{
    public class MsSqlDefaultAzureConnectionProvider : MsSqlAzureConnectionProviderBase
    {
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using Default Azure Credentials to acquire Access Tokens.
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration</param>
        public MsSqlDefaultAzureConnectionProvider(RelationalDatabaseConfiguration configuration) : base(configuration) { }
        
        protected override AccessToken GetAccessTokenFromProvider()
        {
            return GetAccessTokenFromProviderAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        protected override async Task<AccessToken> GetAccessTokenFromProviderAsync(CancellationToken cancellationToken)
        {
            var credential = new DefaultAzureCredential();

            return await credential.GetTokenAsync(new TokenRequestContext(AuthenticationTokenScopes), cancellationToken);
        }
    }
}
