using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql.Azure
{
    public abstract class MsSqlAzureConnectionProviderBase : RelationalDbConnectionProvider
    {
        private readonly bool _cacheTokens;
        private const string AZURE_SCOPE = "https://database.windows.net/.default";
        private const int CACHE_LIFE_TIME = 5;
        
        private readonly string _connectionString;
        protected readonly string[] AuthenticationTokenScopes;
        
        private static AccessToken s_token;
        private static readonly SemaphoreSlim _semaphoreToken = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The Abstract Base class 
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration.</param>
        /// <param name="cacheTokens">Cache Access Tokens until they have less than 5 minutes of life left.</param>
        protected MsSqlAzureConnectionProviderBase(RelationalDatabaseConfiguration configuration, bool cacheTokens = true)
        {
            _cacheTokens = cacheTokens;
            _connectionString = configuration.ConnectionString;
            AuthenticationTokenScopes = new string[1] {AZURE_SCOPE};
        }

        public override DbConnection GetConnection()
        {
            var connection = new SqlConnection(_connectionString);
            connection.AccessToken = GetAccessToken();
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            return connection;
        }

        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(_connectionString);
            connection.AccessToken = await GetAccessTokenAsync(cancellationToken);
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            return connection;
        }

        private string GetAccessToken()
        {
            if (!_cacheTokens) return GetAccessTokenFromProvider().Token;
            _semaphoreToken.Wait();
            try
            {
                //If the Token has more than 5 minutes Validity
                if (DateTime.UtcNow.AddMinutes(CACHE_LIFE_TIME) <= s_token.ExpiresOn.UtcDateTime) return s_token.Token;
        
                var token = GetAccessTokenFromProvider();

                s_token = token;
        
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
                if (DateTime.UtcNow.AddMinutes(CACHE_LIFE_TIME) <= s_token.ExpiresOn.UtcDateTime) return s_token.Token;
        
                var token = await GetAccessTokenFromProviderAsync(cancellationToken);

                s_token = token;
        
                return token.Token;
            }
            finally
            {
                _semaphoreToken.Release();
            }
        }
        
        protected abstract AccessToken GetAccessTokenFromProvider();
        
        protected abstract Task<AccessToken> GetAccessTokenFromProviderAsync(CancellationToken cancellationToken);

    }
}
