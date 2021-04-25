#region Licence
/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

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

namespace Paramore.Brighter.Outbox.MsSql
{
    /// <summary>
    /// Class MsSqlOutboxConfiguration.
    /// </summary>
    public class MsSqlOutboxConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlOutboxConfiguration"/> class.
        /// </summary>
        /// <param name="connectionString">The subscription string.</param>
        /// <param name="outBoxTableName">Name of the outbox table.</param>
        public MsSqlOutboxConfiguration(string connectionString, string outBoxTableName)
        {
            OutBoxTableName = outBoxTableName;
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlOutboxConfiguration"/> class.
        /// </summary>
        /// <param name="connectionString">The subscription string.</param>
        /// <param name="outBoxTableName">Name of the outbox table</param>
        /// <param name="useTokenBasedAuthentication">Use Token Based Authentication to connect to SQL</param>
        /// <param name="authenticationTokenScope">Scope to request the Token for</param>
        public MsSqlOutboxConfiguration(string connectionString, string outBoxTableName,
            bool useTokenBasedAuthentication, string authenticationTokenScope = "https://database.windows.net/.default")
            : this(connectionString, outBoxTableName)
        {
            UseTokenBasedAuthentication = useTokenBasedAuthentication;
            AuthenticationTokenScope = authenticationTokenScope;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MsSqlOutboxConfiguration"/> class.
        /// </summary>
        /// <param name="connectionString">The subscription string.</param>
        /// <param name="outBoxTableName">Name of the outbox table</param>
        /// <param name="useTokenBasedAuthentication">Use Token Based Authentication to connect to SQL</param>
        /// <param name="useSharedTokenCache">Use Access token from Shared Token Cache</param>
        /// <param name="authenticationTokenScope">Scope to request the Token for</param>
        public MsSqlOutboxConfiguration(string connectionString, string outBoxTableName,
            bool useTokenBasedAuthentication, bool useSharedTokenCache,
            string authenticationTokenScope = "https://database.windows.net/.default") : this(connectionString,
            outBoxTableName, useTokenBasedAuthentication, authenticationTokenScope)
        {
            UseSharedTokenCacheCredential = useSharedTokenCache;
        }

        /// <summary>
        /// Gets the subscription string.
        /// </summary>
        /// <value>The subscription string.</value>
        public string ConnectionString { get; private set; }
        /// <summary>
        /// Gets the name of the outbox table.
        /// </summary>
        /// <value>The name of the outbox table.</value>
        public string OutBoxTableName { get; private set; }

        /// <summary>
        /// Use the MSAL Libraries to Authenticate instead of SQL Authentication
        /// </summary>
        public bool UseTokenBasedAuthentication { get; private set; }

        /// <summary>
        /// The Scope to request the Authentication Token for
        /// </summary>
        public string AuthenticationTokenScope { get; private set; }

        /// <summary>
        /// Specifically use Shared Token Cache Credential and specify Username and Tenant
        /// Specifically using the AZURE_USERNAME and AZURE_TENANT_ID Environment Variables
        /// </summary>
        public bool UseSharedTokenCacheCredential { get; private set; }
    }
}
