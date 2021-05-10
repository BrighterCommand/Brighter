using System;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.Inbox.MsSql.ConnectionFactories
{
    public class MsSqlInboxSharedTokenCacheConnectionFactory : IMsSqlInboxConnectionFactory
    {
        private const string _azureUserNameKey = "AZURE_USERNAME";
        private const string _azureTenantIdKey = "AZURE_TENANT_ID";

        private readonly string _connectionString;
        private readonly string[] _authenticationTokenScopes;
        private readonly string _azureUserName;
        private readonly string _azureTenantId;

        public MsSqlInboxSharedTokenCacheConnectionFactory(string connectionString,
            string authenticationTokenScopes = "https://database.windows.net/.default") : this(connectionString,
            Environment.GetEnvironmentVariable(_azureUserNameKey),
            Environment.GetEnvironmentVariable(_azureTenantIdKey), authenticationTokenScopes)
        {
        }

        public MsSqlInboxSharedTokenCacheConnectionFactory(string connectionString, string azureUserName,
            string azureTenantId, string authenticationTokenScopes = "https://database.windows.net/.default")
        {
            _connectionString = connectionString;
            _azureUserName = azureUserName;
            _azureTenantId = azureTenantId;
            _authenticationTokenScopes = new string[1] {authenticationTokenScopes};
        }

        public DbConnection GetConnection()
        {
            var sqlConnection = new SqlConnection(_connectionString);
            var credential = GetCredential();

            var accessToken = credential.GetToken(new TokenRequestContext(_authenticationTokenScopes)).Token;
            sqlConnection.AccessToken = accessToken;

            return sqlConnection;
        }

        public async Task<DbConnection> GetConnectionAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var sqlConnection = new SqlConnection(_connectionString);
            {
                var credential = GetCredential();

                var accessToken =
                    (await credential.GetTokenAsync(
                        new TokenRequestContext(_authenticationTokenScopes),
                        cancellationToken)).Token;
                sqlConnection.AccessToken = accessToken;

                return sqlConnection;
            }
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
