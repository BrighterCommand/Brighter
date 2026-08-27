// The MIT License (MIT)
// Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;

namespace Paramore.Brighter.Oracle;

/// <summary>
/// A connection and transaction provider for Oracle, intended for use with a Unit of Work.
/// The connection and transaction are shared across operations within the same unit of work.
/// </summary>
public class OracleTransactionProvider : RelationalDbTransactionProvider
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the <see cref="OracleTransactionProvider"/> class.
    /// </summary>
    /// <param name="configuration">The relational database configuration containing the connection string.</param>
    /// <exception cref="ArgumentNullException">Thrown when the connection string is null or empty.</exception>
    public OracleTransactionProvider(IAmARelationalDatabaseConfiguration configuration)
    {
        if (string.IsNullOrEmpty(configuration.ConnectionString))
        {
            throw new ArgumentNullException(nameof(configuration.ConnectionString));
        }

        _connectionString = configuration.ConnectionString;
    }

#if !NETFRAMEWORK
    /// <summary>
    /// Commits the current transaction asynchronously and releases it.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    public override async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (HasOpenTransaction)
        {
            var transaction = (OracleTransaction)Transaction!;
            await transaction.CommitAsync(cancellationToken);

            Transaction = null;
        }
    }
#endif

    /// <summary>
    /// Creates and opens a new Oracle connection.
    /// This is not a shared connection; you should manage its lifetime.
    /// </summary>
    /// <returns>An open <see cref="DbConnection"/>.</returns>
    public override DbConnection GetConnection()
    {
        Connection ??= new OracleConnection(_connectionString);
        if (Connection.State != ConnectionState.Open)
        {
            Connection.Open();
        }

        return Connection;
    }

    /// <summary>
    /// Creates and opens a new Oracle connection asynchronously.
    /// This is not a shared connection; you should manage its lifetime.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>An open <see cref="DbConnection"/>.</returns>
    public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        Connection ??= new OracleConnection(_connectionString);
        if (Connection.State != ConnectionState.Open)
        {
            await Connection.OpenAsync(cancellationToken);
        }

        return Connection;
    }

    /// <summary>
    /// Creates and opens a shared Oracle transaction.
    /// Reuses the existing transaction if one is already open.
    /// </summary>
    /// <returns>The shared <see cref="DbTransaction"/>.</returns>
    public override DbTransaction GetTransaction()
    {
        Connection ??= GetConnection();

        if (!HasOpenTransaction)
        {
            Transaction = ((OracleConnection)Connection).BeginTransaction();
        }

        return Transaction!;
    }

    /// <summary>
    /// Creates and opens a shared Oracle transaction asynchronously.
    /// Reuses the existing transaction if one is already open.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The shared <see cref="DbTransaction"/>.</returns>
    public override async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        Connection ??= await GetConnectionAsync(cancellationToken);

        if (!HasOpenTransaction)
        {
#if NETFRAMEWORK
            Transaction = ((OracleConnection)Connection).BeginTransaction();
#else
            Transaction = await ((OracleConnection)Connection).BeginTransactionAsync(cancellationToken);
#endif
        }

        return Transaction!;
    }
}
