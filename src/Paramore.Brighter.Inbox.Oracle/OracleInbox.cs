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
using Oracle.ManagedDataAccess.Client;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Oracle;

namespace Paramore.Brighter.Inbox.Oracle;

/// <summary>
/// Implements an Inbox using Oracle as a backing store.
/// Requires Oracle 12c or later.
/// </summary>
public class OracleInbox : RelationalDatabaseInbox
{
    // ORA-00001: unique constraint violated
    private const int DuplicateKeyError = 1;

    /// <summary>
    /// Initializes a new instance of the <see cref="OracleInbox"/> class.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <param name="connectionProvider">The connection provider.</param>
    public OracleInbox(IAmARelationalDatabaseConfiguration configuration,
        IAmARelationalDbConnectionProvider connectionProvider)
        : base(DbSystem.Oracle, configuration, connectionProvider, new OracleQueries(),
            ApplicationLogging.CreateLogger<OracleInbox>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OracleInbox"/> class using a default connection provider.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    public OracleInbox(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, new OracleConnectionProvider(configuration))
    {
    }

    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
    {
        return ex is OracleException { Number: DuplicateKeyError };
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
    {
        parameterName = parameterName.Replace("@", ":");
        return new OracleParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uses <see cref="OracleDbType.Clob"/> rather than <c>OracleDbType.Json</c> because
    /// the native Oracle JSON type is only available from Oracle Database 21c onwards.
    /// Oracle 19c stores JSON as a <c>CLOB</c> column validated by an <c>IS JSON</c> check constraint.
    /// </remarks>
    protected override IDbDataParameter CreateJsonSqlParameter(string parameterName, object? value)
    {
        parameterName = parameterName.Replace("@", ":");
        return new OracleParameter
        {
            ParameterName = parameterName, Value = value ?? DBNull.Value, OracleDbType = OracleDbType.Clob
        };
    }

    /// <inheritdoc />
    /// <remarks>Sets <see cref="OracleCommand.BindByName"/> to <see langword="true"/> so that
    /// named <c>:param</c> placeholders in the SQL are matched to parameters by name.</remarks>
    protected override DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
        params IDbDataParameter[] parameters)
    {
        var command = (OracleCommand)connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
        command.CommandText = sqlText;
        command.Parameters.AddRange(parameters);
        return command;
    }

}
