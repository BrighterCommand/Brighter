using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.MessagingGateway.MsSql.ConnectionFactories
{
    public class MsSqlMessagingGatewayDefaultAzureConnectionFactory : IMsSqlMessagingGatewayConnectionFactory
    {
        private readonly string _connectionString;
        private readonly string[] _authenticationTokenScopes;
        public MsSqlMessagingGatewayDefaultAzureConnectionFactory(string connectionString, string authenticationTokenScopes = "https://database.windows.net/.default")
        {
            _connectionString = connectionString;
            _authenticationTokenScopes = new string[1] {authenticationTokenScopes};
        }

        public SqlConnection GetConnection()
        {
            var sqlConnection = new SqlConnection(_connectionString);
            var credential = new DefaultAzureCredential();
            var accessToken = credential.GetToken(new TokenRequestContext(_authenticationTokenScopes)).Token;
            sqlConnection.AccessToken = accessToken;

            return sqlConnection;
        }

        public async Task<SqlConnection> GetConnectionAsync(
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
