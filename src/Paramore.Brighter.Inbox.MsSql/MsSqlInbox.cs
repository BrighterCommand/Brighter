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
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Inbox.MsSql;

/// <summary>
///     Class MsSqlInbox.
/// </summary>
public class MsSqlInbox : RelationalDatabaseInbox
{
    private const int MsSqlDuplicateKeyError_UniqueIndexViolation = 2601;
    private const int MsSqlDuplicateKeyError_UniqueConstraintViolation = 2627;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MsSqlInbox" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="connectionProvider">The Connection Provider.</param>
    public MsSqlInbox(IAmARelationalDatabaseConfiguration configuration, IAmARelationalDbConnectionProvider connectionProvider) 
        : base(DbSystem.MsSql, configuration, connectionProvider,
            new MsSqlQueries(), ApplicationLogging.CreateLogger<MsSqlInbox>())
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MsSqlInbox" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public MsSqlInbox(IAmARelationalDatabaseConfiguration configuration) : this(configuration,
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
}
