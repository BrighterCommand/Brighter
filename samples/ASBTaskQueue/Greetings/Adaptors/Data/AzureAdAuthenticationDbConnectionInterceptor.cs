using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Greetings.Adaptors.Data
{
    public class AzureAdAuthenticationDbConnectionInterceptor : DbConnectionInterceptor
    {
        // See https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/services-support-managed-identities#azure-sql
        private static readonly string[] _azureSqlScopes = new[] { "https://database.windows.net//.default" };
        private const int _cacheLifeTime = 5;

        private static readonly TokenCredential _credential = new ChainedTokenCredential(
            new ManagedIdentityCredential(),
            new VisualStudioCredential());

        private static AccessToken _token;
        private static SemaphoreSlim _semaphoreToken = new SemaphoreSlim(1, 1);

        public override InterceptionResult ConnectionOpening(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
        {
            var sqlConnection = (SqlConnection)connection;
            if (DoesConnectionNeedAccessToken(sqlConnection))
                sqlConnection.AccessToken = GetAccessToken();

            return base.ConnectionOpening(connection, eventData, result);
        }

        public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            var sqlConnection = (SqlConnection)connection;
            if (DoesConnectionNeedAccessToken(sqlConnection))
                sqlConnection.AccessToken = await GetAccessTokenAsync();

            return await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
        }

        private static bool DoesConnectionNeedAccessToken(SqlConnection connection)
        {
            //
            // Only try to get a token from AAD if
            //  - We connect to an Azure SQL instance; and
            //  - The connection doesn't specify a username.
            //
            var connectionStringBuilder = new SqlConnectionStringBuilder(connection.ConnectionString);

            return connectionStringBuilder.DataSource.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(connectionStringBuilder.UserID);
        }

        private string GetAccessToken()
        {
            _semaphoreToken.Wait();
            try
            {
                //If the Token has more than 5 minutes Validity
                if (DateTime.UtcNow.AddMinutes(_cacheLifeTime) <= _token.ExpiresOn.UtcDateTime)
                    return _token.Token;

                var tokenRequestContext = new TokenRequestContext(_azureSqlScopes);
                var token = _credential.GetToken(tokenRequestContext, CancellationToken.None);

                _token = token;

                return token.Token;
            }
            finally
            {
                _semaphoreToken.Release();
            }
        }

        private async Task<string> GetAccessTokenAsync()
        {
            await _semaphoreToken.WaitAsync();
            try
            {
                //If the Token has more than 5 minutes Validity
                if (DateTime.UtcNow.AddMinutes(_cacheLifeTime) <= _token.ExpiresOn.UtcDateTime)
                    return _token.Token;

                var tokenRequestContext = new TokenRequestContext(_azureSqlScopes);
                var token = await _credential.GetTokenAsync(tokenRequestContext, CancellationToken.None);

                _token = token;

                return token.Token;
            }
            finally
            {
                _semaphoreToken.Release();
            }
        }
    }
}
