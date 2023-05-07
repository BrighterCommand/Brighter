using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql.Azure
{
    public abstract class MsSqlAzureConnectonProviderBase : IAmATransactionConnectonProvider
    {
        private readonly bool _cacheTokens;
        private const string _azureScope = "https://database.windows.net/.default";
        private const int _cacheLifeTime = 5;
        
        private readonly string _connectionString;
        protected readonly string[] _authenticationTokenScopes;
        
        private static AccessToken _token;
        private static SemaphoreSlim _semaphoreToken = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The Abstract Base class 
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration.</param>
        /// <param name="cacheTokens">Cache Access Tokens until they have less than 5 minutes of life left.</param>
        protected MsSqlAzureConnectonProviderBase(RelationalDatabaseConfiguration configuration, bool cacheTokens = true)
        {
            _cacheTokens = cacheTokens;
            _connectionString = configuration.ConnectionString;
            _authenticationTokenScopes = new string[1] {_azureScope};
        }

        public DbConnection GetConnection()
        {
            var sqlConnection = new SqlConnection(_connectionString);
            sqlConnection.AccessToken = GetAccessToken();

            return sqlConnection;
        }

        public async Task<DbConnection> GetConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            var sqlConnection = new SqlConnection(_connectionString);
            sqlConnection.AccessToken = await GetAccessTokenAsync(cancellationToken);

            return sqlConnection;
        }

        private string GetAccessToken()
        {
            if (!_cacheTokens) return GetAccessTokenFromProvider().Token;
            _semaphoreToken.Wait();
            try
            {
                //If the Token has more than 5 minutes Validity
                if (DateTime.UtcNow.AddMinutes(_cacheLifeTime) <= _token.ExpiresOn.UtcDateTime) return _token.Token;
        
                var credential = new ManagedIdentityCredential();
                var token = GetAccessTokenFromProvider();

                _token = token;
        
                return token.Token;
            }
            finally
            {
                _semaphoreToken.Release();
            }
        }
        
        private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            if (!_cacheTokens) return (await GetAccessTokenFromProviderAsync(cancellationToken)).Token;
            await _semaphoreToken.WaitAsync(cancellationToken);
            try
            {
                //If the Token has more than 5 minutes Validity
                if (DateTime.UtcNow.AddMinutes(_cacheLifeTime) <= _token.ExpiresOn.UtcDateTime) return _token.Token;
        
                var credential = new ManagedIdentityCredential();
                var token = await GetAccessTokenFromProviderAsync(cancellationToken);

                _token = token;
        
                return token.Token;
            }
            finally
            {
                _semaphoreToken.Release();
            }
        }
        
        protected abstract AccessToken GetAccessTokenFromProvider();
        
        protected abstract Task<AccessToken> GetAccessTokenFromProviderAsync(CancellationToken cancellationToken);

        public DbTransaction GetTransaction()
        {
            //This Connection Factory does not support Transactions 
            return null;
        }

        public bool HasOpenTransaction { get => false; }
        public bool IsSharedConnection { get => false; }
    }
}
