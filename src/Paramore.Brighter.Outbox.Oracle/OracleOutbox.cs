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
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Oracle.ManagedDataAccess.Client;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Oracle;

namespace Paramore.Brighter.Outbox.Oracle;

/// <summary>
/// Implements an Outbox using Oracle as a backing store.
/// Requires Oracle 12c or later.
/// </summary>
public class OracleOutbox : RelationDatabaseOutbox
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OracleOutbox"/> class.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    /// <param name="connectionProvider">The connection provider.</param>
    public OracleOutbox(IAmARelationalDatabaseConfiguration configuration,
        IAmARelationalDbConnectionProvider connectionProvider)
        : base(DbSystem.Oracle, configuration, connectionProvider, new OracleQueries(),
            ApplicationLogging.CreateLogger<OracleOutbox>())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OracleOutbox"/> class using a default connection provider.
    /// </summary>
    /// <param name="configuration">The relational database configuration.</param>
    public OracleOutbox(IAmARelationalDatabaseConfiguration configuration)
        : this(configuration, new OracleConnectionProvider(configuration))
    {
    }

    // ORA-00001: unique constraint violated
    private const int DuplicateValue = 1;

    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
    {
        return ex is OracleException { Number: DuplicateValue };
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
    {
        parameterName = parameterName.Replace("@", ":");
        return new OracleParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
    {
        parameterName = parameterName.Replace("@", ":");
        return new OracleParameter { ParameterName = parameterName, DbType = dbType, Value = value ?? DBNull.Value };
    }

    /// <inheritdoc />
    /// <remarks>Sets <see cref="OracleCommand.BindByName"/> to <see langword="true"/> so that
    /// named <c>:param</c> placeholders in the SQL are matched to parameters by name.</remarks>
    protected override DbCommand CreateCommand(DbConnection connection, string sqlText, int outBoxTimeout,
        params IDbDataParameter[] parameters)
    {
        var oracleConnection = (OracleConnection)connection;
        var command = oracleConnection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = outBoxTimeout < 0 ? 0 : outBoxTimeout;
        command.CommandText = sqlText;
        command.Parameters.AddRange(parameters);
        return command;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Generates <c>SELECT … FROM DUAL UNION ALL SELECT … FROM DUAL</c> rather than
    /// multi-row <c>VALUES</c> tuples, which Oracle does not support before version 23c.
    /// </remarks>
    protected override (string insertClause, IDbDataParameter[] parameters) GenerateBulkInsert(List<Message> messages)
    {
        var selects = new List<string>();
        var parameters = new List<IDbDataParameter>();

        for (var i = 0; i < messages.Count; i++)
        {
            selects.Add(
                $"SELECT :p{i}_MessageId,:p{i}_MessageType,:p{i}_Topic,:p{i}_Timestamp,:p{i}_CorrelationId," +
                $":p{i}_ReplyTo,:p{i}_ContentType,:p{i}_PartitionKey,:p{i}_HeaderBag,:p{i}_Body," +
                $":p{i}_Source,:p{i}_Type,:p{i}_DataSchema,:p{i}_Subject,:p{i}_TraceParent,:p{i}_TraceState," +
                $":p{i}_Baggage,:p{i}_WorkflowId,:p{i}_JobId FROM DUAL");
            parameters.AddRange(InitAddDbParameters(messages[i], i));
        }

        return (string.Join(" UNION ALL ", selects), parameters.ToArray());
    }

    protected override (string inClause, IDbDataParameter[] parameters) GenerateInClauseAndAddParameters(
        List<string> messageIds)
    {
        var paramNames = messageIds.Select((_, i) => ":p" + i).ToArray();

        var parameters = new IDbDataParameter[messageIds.Count];
        for (int i = 0; i < paramNames.Length; i++)
        {
            parameters[i] = CreateSqlParameter(paramNames[i], DbType.String, messageIds[i]);
        }

        return (string.Join(",", paramNames), parameters);
    }

    /// <inheritdoc />
    protected override DateTimeOffset GetTimeStamp(DbDataReader dr)
    {
        if (!TryGetOrdinal(dr, TimestampColumnName, out var ordinal) || dr.IsDBNull(ordinal))
        {
            return DateTimeOffset.MinValue;
        }

        var reader = (OracleDataReader)dr;
        return reader.GetDateTimeOffset(ordinal);
    }
}
