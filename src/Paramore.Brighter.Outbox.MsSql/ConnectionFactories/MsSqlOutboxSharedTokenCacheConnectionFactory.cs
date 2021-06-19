using System;
using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.Outbox.MsSql.ConnectionFactories
{
    public class MsSqlOutboxSharedTokenCacheConnectionFactory : IMsSqlOutboxConnectionFactory
    {
        private const string _azureScope = "https://database.windows.net/.default";
        private const string _azureUserNameKey = "AZURE_USERNAME";
        private const string _azureTenantIdKey = "AZURE_TENANT_ID";

        private readonly string _connectionString;
        private readonly string[] _authenticationTokenScopes;
        private readonly string _azureUserName;
        private readonly string _azureTenantId;

        public MsSqlOutboxSharedTokenCacheConnectionFactory(MsSqlOutboxConfiguration configuration)
        {
            _connectionString = configuration.ConnectionString;
            _authenticationTokenScopes = new string[1] {_azureScope};
            IsScoped = configuration.IsScoped;
        }

        public MsSqlOutboxSharedTokenCacheConnectionFactory(MsSqlOutboxConfiguration configuration, string azureUserName,
            string azureTenantId) : this(configuration)
        {
            _azureUserName = azureUserName;
            _azureTenantId = azureTenantId;
        }

        public bool IsScoped { get; }

        public SqlConnection GetConnection()
        {
            var sqlConnection = new SqlConnection(_connectionString);
            var credential = GetCredential();

            var accessToken = credential.GetToken(new TokenRequestContext(_authenticationTokenScopes)).Token;
            sqlConnection.AccessToken = accessToken;

            return sqlConnection;
        }

        public async Task<SqlConnection> GetConnectionAsync(
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
