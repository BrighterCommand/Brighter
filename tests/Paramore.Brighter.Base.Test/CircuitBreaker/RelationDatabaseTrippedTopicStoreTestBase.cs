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

using System;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Base.Test.CircuitBreaker;

public abstract class RelationDatabaseTrippedTopicStoreTestBase : IAsyncLifetime
{
    private IAmATrippedTopicStore? _store;

    protected string TableName { get; } = $"trips_{Guid.NewGuid():N}";
    protected IAmATrippedTopicStore Store => _store!;

    protected abstract IAmARelationalDbConnectionProvider ConnectionProvider { get; }
    protected abstract string CreateTableSql { get; }
    protected abstract string DropTableSql { get; }
    protected abstract IAmATrippedTopicStore CreateStore();

    public async Task InitializeAsync()
    {
        await ExecuteAsync(CreateTableSql);
        _store = CreateStore();
    }

    public async Task DisposeAsync() => await ExecuteAsync(DropTableSql);

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = await ConnectionProvider.GetConnectionAsync(CancellationToken.None);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
