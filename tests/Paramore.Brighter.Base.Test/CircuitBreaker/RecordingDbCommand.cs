#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Base.Test.CircuitBreaker;

public class RecordingDbCommand(DbCommand inner, DbConnection connection, List<RecordedCommand> executed) : DbCommand
{
    [AllowNull]
    public override string CommandText { get => inner.CommandText; set => inner.CommandText = value; }
    public override int CommandTimeout { get => inner.CommandTimeout; set => inner.CommandTimeout = value; }
    public override CommandType CommandType { get => inner.CommandType; set => inner.CommandType = value; }
    public override bool DesignTimeVisible { get => inner.DesignTimeVisible; set => inner.DesignTimeVisible = value; }
    public override UpdateRowSource UpdatedRowSource { get => inner.UpdatedRowSource; set => inner.UpdatedRowSource = value; }
    protected override DbConnection? DbConnection { get => connection; set { } }
    protected override DbParameterCollection DbParameterCollection => inner.Parameters;
    protected override DbTransaction? DbTransaction { get => inner.Transaction; set => inner.Transaction = value; }

    public override void Cancel() => inner.Cancel();
    public override void Prepare() => inner.Prepare();
    protected override DbParameter CreateDbParameter() => inner.CreateParameter();

    public override int ExecuteNonQuery()
    {
        Record();
        return inner.ExecuteNonQuery();
    }

    public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
    {
        Record();
        return inner.ExecuteNonQueryAsync(cancellationToken);
    }

    public override object? ExecuteScalar()
    {
        Record();
        return inner.ExecuteScalar();
    }

    public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken)
    {
        Record();
        return inner.ExecuteScalarAsync(cancellationToken);
    }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        Record();
        return inner.ExecuteReader(behavior);
    }

    protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
    {
        Record();
        return inner.ExecuteReaderAsync(behavior, cancellationToken);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }

    private void Record() => executed.Add(new RecordedCommand(inner.CommandText, inner.CommandTimeout));
}
