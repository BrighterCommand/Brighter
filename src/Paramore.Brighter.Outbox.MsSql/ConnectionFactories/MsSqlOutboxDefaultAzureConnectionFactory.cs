using Microsoft.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.Outbox.MsSql.ConnectionFactories
{
    public class MsSqlOutboxDefaultAzureConnectionFactory : IMsSqlOutboxConnectionFactory
    {
        private const string _azureScope = "https://database.windows.net/.default";
        
        private readonly string _connectionString;
        private readonly string[] _authenticationTokenScopes;
        public MsSqlOutboxDefaultAzureConnectionFactory(MsSqlOutboxConfiguration configuration)
        {
            _connectionString = configuration.ConnectionString;
            _authenticationTokenScopes = new string[1] {_azureScope};
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

        public SqlTransaction GetTransaction()
        {
            //This Connection Factory does not support Transactions 
            return null;
        }

        public bool HasOpenTransaction { get => false; }
        public bool IsSharedConnection { get => false; }
    }
}
