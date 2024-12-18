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

using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;

namespace Paramore.Brighter.MsSql.Azure
{
    /// <summary>
    /// Provides a connection to an MS SQL database using Managed Identity Credentials to acquire Access Tokens.
    /// </summary>
    public class MsSqlManagedIdentityConnectionProvider : MsSqlAzureConnectionProviderBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlManagedIdentityConnectionProvider"/> class.
        /// </summary>
        /// <param name="configuration">The MS SQL configuration.</param>
        public MsSqlManagedIdentityConnectionProvider(RelationalDatabaseConfiguration configuration) : base(configuration)
        {
        }

        /// <summary>
        /// Gets the access token from the provider synchronously.
        /// Sync over async, but alright in the context of creating a connection.
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
            var credential = new ManagedIdentityCredential();
            return await credential.GetTokenAsync(new TokenRequestContext(AuthenticationTokenScopes), cancellationToken);
        }
    }
}
