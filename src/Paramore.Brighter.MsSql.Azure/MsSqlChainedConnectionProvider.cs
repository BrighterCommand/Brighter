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
    public class ServiceBusChainedClientConnectionProvider : MsSqlAzureConnectionProviderBase
    {
        private readonly ChainedTokenCredential _credential;
        
        /// <summary>
        /// Initialise a new instance of Ms Sql Connection provider using Default Azure Credentials to acquire Access Tokens.
        /// </summary>
        /// <param name="configuration">Ms Sql Configuration</param>
        /// <param name="credentialSources">List of Token Providers to use when trying to obtain a token.</param>
        public ServiceBusChainedClientConnectionProvider(RelationalDatabaseConfiguration configuration,
            params TokenCredential[] credentialSources) : base(configuration)
        {
            if (credentialSources == null || credentialSources.Length < 1)
            {
                throw new ArgumentNullException(nameof(credentialSources),
                    "Credential Sources is null or empty, ensure this is set in the constructor.");
            }

            _credential = new ChainedTokenCredential(credentialSources);
        }
        
        /// <summary>
        /// Get Access Token from the Provider synchronously.
        /// Sync over Async,  but alright in the context of creating a connection.
        /// </summary>
        /// <returns>The access token</returns>
        protected override AccessToken GetAccessTokenFromProvider()
        {
            return GetAccessTokenFromProviderAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Get Access Token from the Provider asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancels the read of the connection</param>
        /// <returns></returns>
        protected override async Task<AccessToken> GetAccessTokenFromProviderAsync(CancellationToken cancellationToken)
        {
            return await _credential.GetTokenAsync(new TokenRequestContext(AuthenticationTokenScopes), cancellationToken);
        }
    }
}
