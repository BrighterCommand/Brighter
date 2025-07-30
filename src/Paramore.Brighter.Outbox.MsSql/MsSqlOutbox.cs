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
using System.Linq;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Outbox.MsSql;

/// <summary>
/// Implements an Outbox using MSSQL as a backing store 
/// </summary>
public class MsSqlOutbox : RelationDatabaseOutbox
{
    private const int MsSqlDuplicateKeyError_UniqueIndexViolation = 2601;
    private const int MsSqlDuplicateKeyError_UniqueConstraintViolation = 2627;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MsSqlOutbox" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="connectionProvider">The connection factory.</param>
    public MsSqlOutbox(IAmARelationalDatabaseConfiguration configuration,
        IAmARelationalDbConnectionProvider connectionProvider) 
        : base(DbSystem.MsSql, configuration, connectionProvider,
            new MsSqlQueries(), ApplicationLogging.CreateLogger<MsSqlOutbox>())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MsSqlOutbox" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public MsSqlOutbox(IAmARelationalDatabaseConfiguration configuration) : this(configuration,
        new MsSqlConnectionProvider(configuration))
    {
    }

    /// <inheritdoc />
    protected override bool IsExceptionUniqueOrDuplicateIssue(Exception ex)
    {
        return ex is SqlException { Number: MsSqlDuplicateKeyError_UniqueIndexViolation or MsSqlDuplicateKeyError_UniqueConstraintViolation };
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, object? value)
    {
        return new SqlParameter { ParameterName = parameterName, Value = value ?? DBNull.Value };
    }

    /// <inheritdoc />
    protected override IDbDataParameter CreateSqlParameter(string parameterName, DbType dbType, object? value)
    {
        return new SqlParameter { ParameterName = parameterName, Value = value ?? DBNull.Value, DbType = dbType };
    }
    
   protected override IDbDataParameter[] CreatePagedOutstandingParameters(TimeSpan since, int pageSize, 
       int pageNumber, IDbDataParameter[] inParams)
   {
       var parameters = new IDbDataParameter[3];
       parameters[0] = new SqlParameter { ParameterName = "PageNumber", Value = pageNumber };
       parameters[1] = new SqlParameter { ParameterName = "PageSize", Value = pageSize };
       parameters[2] = CreateSqlParameter("DispatchedSince", DateTimeOffset.UtcNow.Subtract(since));
       
       return parameters.Concat(inParams).ToArray();
    }
   
    protected override IDbDataParameter[] CreatePagedDispatchedParameters(TimeSpan dispatchedSince, int pageSize, int pageNumber)
    {
        var parameters = new IDbDataParameter[3];
        parameters[0] = new SqlParameter { ParameterName = "PageNumber", Value = pageNumber };
        parameters[1] = new SqlParameter { ParameterName = "PageSize", Value = pageSize };
        parameters[2] = CreateSqlParameter("DispatchedSince", DateTimeOffset.UtcNow.Subtract(dispatchedSince));
   
        return parameters;
    }
   
    protected override IDbDataParameter[] CreatePagedReadParameters(int pageSize, int pageNumber)
    {
        var parameters = new IDbDataParameter[2];
        parameters[0] = new SqlParameter { ParameterName = "PageNumber", Value = pageNumber };
        parameters[1] = new SqlParameter { ParameterName = "PageSize", Value = pageSize };
        return parameters;
    } 
}
