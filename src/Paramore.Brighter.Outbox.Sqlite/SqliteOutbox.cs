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

using System;
using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Sqlite;

namespace Paramore.Brighter.Outbox.Sqlite;

/// <summary>
/// Implements an outbox using Sqlite as a backing store
/// </summary>
public class SqliteOutbox : RelationDatabaseOutbox
{
    private const int SqliteDuplicateKeyError = 1555;
    private const int SqliteUniqueKeyError = 19;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteOutbox" /> class.
    /// </summary>
    /// <param name="configuration">The configuration to connect to this data store</param>
    /// <param name="connectionProvider">Provides a connection to the Db that allows us to enlist in an ambient transaction</param>
    public SqliteOutbox(
        IAmARelationalDatabaseConfiguration configuration,
        IAmARelationalDbConnectionProvider connectionProvider
    )
        : base(DbSystem.Sqlite, configuration, connectionProvider, 
            new SqliteQueries(), ApplicationLogging.CreateLogger<SqliteOutbox>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteOutbox" /> class.
    /// </summary>
    /// <param name="configuration">The configuration to connect to this data store</param>
    public SqliteOutbox(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, new SqliteConnectionProvider(configuration))
    {
    }

    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
    {
        return ex is SqliteException { SqliteErrorCode: SqliteDuplicateKeyError or SqliteUniqueKeyError };
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
    {
        return new SqliteParameter(parameterName, value ?? DBNull.Value);
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
    {
        return new SqliteParameter(parameterName, value ?? DBNull.Value) { DbType = dbType };
    }

    /// <inheritdoc />
    protected override DateTimeOffset GetTimeStamp(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, TimestampColumnName, out var ordinal) || dr.IsDBNull(ordinal))
        {
            return DateTimeOffset.MinValue;
        }

        var reader = (SqliteDataReader)dr;
        var dataTime = reader.GetDateTimeOffset(ordinal);
        return dataTime; 
    }
}
