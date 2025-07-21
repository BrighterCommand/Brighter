#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.MsSql.Azure
{
    /// <summary>
    /// Provides a connection to an MS SQL database using Shared Token Cache Credentials to acquire Access Tokens.
    /// </summary>
    public class MsSqlSharedTokenCacheConnectionProvider : MsSqlAzureConnectionProviderBase
    {
        private const string _azureUserNameKey = "AZURE_USERNAME";
        private const string _azureTenantIdKey = "AZURE_TENANT_ID";

        private readonly string _azureUserName;
        private readonly string _azureTenantId;

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlSharedTokenCacheConnectionProvider"/> class using environment variables for credentials.
        /// </summary>
        /// <param name="configuration">The MS SQL configuration.</param>
        public MsSqlSharedTokenCacheConnectionProvider(RelationalDatabaseConfiguration configuration) : base(configuration)
        {
            _azureUserName = Environment.GetEnvironmentVariable(_azureUserNameKey) ?? throw new InvalidOperationException("Azure username environment variable not set.");
            _azureTenantId = Environment.GetEnvironmentVariable(_azureTenantIdKey) ?? throw new InvalidOperationException("Azure tenant ID environment variable not set.");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlSharedTokenCacheConnectionProvider"/> class using specified credentials.
        /// </summary>
        /// <param name="configuration">The MS SQL configuration.</param>
        /// <param name="userName">The Azure username.</param>
        /// <param name="tenantId">The Azure tenant ID.</param>
        public MsSqlSharedTokenCacheConnectionProvider(RelationalDatabaseConfiguration configuration, string userName, string tenantId) : base(configuration)
        {
            _azureUserName = userName;
            _azureTenantId = tenantId;
        }

        /// <summary>
        /// Gets the access token from the provider synchronously.
        /// Sync over async, but alright in the context of a connection provider.
        /// </summary>
        /// <returns>The access token.</returns>
        protected override AccessToken GetAccessTokenFromProvider()
        {
            return GetAccessTokenFromProviderAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Gets the access token from the provider asynchronously.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the access token.</returns>
        protected override async Task<AccessToken> GetAccessTokenFromProviderAsync(CancellationToken cancellationToken)
        {
            var credential = GetCredential();
            return await credential.GetTokenAsync(new TokenRequestContext(AuthenticationTokenScopes), cancellationToken);
        }

        /// <summary>
        /// Gets the shared token cache credential.
        /// </summary>
        /// <returns>The shared token cache credential.</returns>
        private SharedTokenCacheCredential GetCredential()
        {
            return new SharedTokenCacheCredential(new SharedTokenCacheCredentialOptions
            {
                Username = _azureUserName,
                TenantId = _azureTenantId,
            });
        }
    }
}
