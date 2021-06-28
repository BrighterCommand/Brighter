using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql.Azure
{
    public abstract class MsSqlAzureConnectionProviderBase
    {
        private const string _azureScope = "https://database.windows.net/.default";
        
        private readonly string _connectionString;
        protected readonly string[] _authenticationTokenScopes;

        protected MsSqlAzureConnectionProviderBase(MsSqlConfiguration configuration)
        {
            _connectionString = configuration.ConnectionString;
            _authenticationTokenScopes = new string[1] {_azureScope};
        }

        public SqlConnection GetConnection()
        {
            var sqlConnection = new SqlConnection(_connectionString);
            var accessToken = GetAccessToken();
            sqlConnection.AccessToken = accessToken.Token;

            return sqlConnection;
        }

        public async Task<SqlConnection> GetConnectionAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var sqlConnection = new SqlConnection(_connectionString);
            var accessToken = await GetAccessTokenAsync(cancellationToken);
            sqlConnection.AccessToken = accessToken.Token;

            return sqlConnection;
        }

        protected abstract AccessToken GetAccessToken();
        
        protected abstract Task<AccessToken> GetAccessTokenAsync(CancellationToken cancellationToken);

        public SqlTransaction GetTransaction()
        {
            //This Connection Factory does not support Transactions 
            return null;
        }

        public bool HasOpenTransaction { get => false; }
        public bool IsSharedConnection { get => false; }
    }
}
