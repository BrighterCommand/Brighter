#region Licence

/* The MIT License (MIT)
Copyright © 2020 Ian Cooper <ian.cooper@yahoo.co.uk>

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
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NpgsqlTypes;
using Paramore.Brighter.Observability;
using Paramore.Brighter.PostgreSql;

namespace Paramore.Brighter.Inbox.Postgres;

public class PostgreSqlInbox : RelationalDatabaseInbox
{
    public PostgreSqlInbox(IAmARelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider connectionProvider, ILogger? logger = null)
        : base(DbSystem.Postgresql, configuration, connectionProvider,
            new PostgreSqlQueries(), logger ?? NullLogger<PostgreSqlInbox>.Instance)
    {
    }

    public PostgreSqlInbox(IAmARelationalDatabaseConfiguration configuration, ILogger? logger = null)
        : this(configuration, new PostgreSqlConnectionProvider(configuration), logger)
    {
    }


    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
    {
        return ex is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
    {
        return new NpgsqlParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
    }

    protected override IDbDataParameter CreateJsonSqlParameter(string parameterName, object? value)
    {
        return new NpgsqlParameter { ParameterName = parameterName, NpgsqlDbType = DatabaseConfiguration.BinaryMessagePayload ? NpgsqlDbType.Jsonb : NpgsqlDbType.Json,Value = value ?? DBNull.Value };
    }

    /// <summary>
    /// Lowercase-then-quote the configured inbox table name so reserved-keyword names
    /// (Order, User, Group, …) parse cleanly and mixed-case configured values resolve to
    /// the same physical table as PG's natural case-fold of the legacy unquoted form would
    /// have produced.
    /// </summary>
    protected override string GenerateSqlText(string sqlFormat, params string[] orderedParams)
        => string.Format(
            sqlFormat,
            orderedParams.Prepend(PgIdentifier.Quote(DatabaseConfiguration.InBoxTableName)).ToArray());
}
