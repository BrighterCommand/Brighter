using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.Outbox.MsSql.ConnectionFactories
{
    public class MsSqlOutboxDefaultAzureConnectionFactory : IMsSqlOutboxConnectionFactory
    {
        private readonly string _connectionString;
        private readonly string[] _authenticationTokenScopes;
        public MsSqlOutboxDefaultAzureConnectionFactory(string connectionString, string authenticationTokenScopes = "https://database.windows.net/.default")
        {
            _connectionString = connectionString;
            _authenticationTokenScopes = new string[1] {authenticationTokenScopes};
        }

        public DbConnection GetConnection()
        {
            var sqlConnection = new SqlConnection(_connectionString);
            var credential = new DefaultAzureCredential();
            var accessToken = credential.GetToken(new TokenRequestContext(_authenticationTokenScopes)).Token;
            sqlConnection.AccessToken = accessToken;

            return sqlConnection;
        }

        public async Task<DbConnection> GetConnectionAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var sqlConnection = new SqlConnection(_connectionString);
            var credential = new DefaultAzureCredential();
            var accessToken =
                (await credential.GetTokenAsync(
                    new TokenRequestContext(_authenticationTokenScopes),
                    cancellationToken)).Token;
            sqlConnection.AccessToken = accessToken;

            return sqlConnection;
        }
    }
}
