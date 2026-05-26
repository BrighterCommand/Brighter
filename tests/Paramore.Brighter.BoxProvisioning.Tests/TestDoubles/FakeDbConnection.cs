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

#nullable enable

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;

/// <summary>
/// Minimal <see cref="DbConnection"/> stub used as the <c>TConnection</c> type-argument
/// when exercising <c>SqlBoxMigrationRunner</c> without a real database. The
/// runner base treats the connection as an opaque token threaded through hooks, so all
/// data-access members throw <see cref="NotSupportedException"/>; lifecycle members
/// (<see cref="Close"/>, <see cref="Open"/>) update an in-memory state field so
/// <c>using</c>/<c>await using</c> dispose paths complete cleanly.
/// </summary>
internal class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;
    public override string Database => string.Empty;
    public override string DataSource => string.Empty;
    public override string ServerVersion => string.Empty;
    public override ConnectionState State => _state;

    public override void ChangeDatabase(string databaseName) { }
    public override void Close() => _state = ConnectionState.Closed;
    public override void Open() => _state = ConnectionState.Open;

    protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
}
