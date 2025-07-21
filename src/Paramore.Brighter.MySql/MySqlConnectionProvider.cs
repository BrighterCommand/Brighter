#region Licence

/* The MIT License (MIT)
Copyright © 2021 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySqlConnector;

namespace Paramore.Brighter.MySql
{
    /// <summary>
    /// A connection provider that uses the connection string to create a connection
    /// </summary>
    public class MySqlConnectionProvider : RelationalDbConnectionProvider
    {
        private readonly string _connectionString;

        /// <summary>
        /// Initialise a new instance of MySql Connection provider from a connection string
        /// </summary>
        /// <param name="configuration">MySql Configuration</param>
        public MySqlConnectionProvider(IAmARelationalDatabaseConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration?.ConnectionString))
                throw new ArgumentNullException(nameof(configuration.ConnectionString));
            _connectionString = configuration!.ConnectionString;
        }

        /// <summary>
        /// Creates and opens a MySql Connection
        /// This is not a shared connection and you should manage its lifetime
        /// </summary>
        /// <returns></returns>
        public override DbConnection GetConnection()
        {
            var connection = new MySqlConnection(_connectionString);
            if (connection.State != ConnectionState.Open) connection.Open();
            return connection;
        }

        /// <summary>
        /// Creates and opens a MySql Connection
        /// This is not a shared connection and you should manage its lifetime
        /// </summary>
        /// <returns></returns>
        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection(_connectionString);
            if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
