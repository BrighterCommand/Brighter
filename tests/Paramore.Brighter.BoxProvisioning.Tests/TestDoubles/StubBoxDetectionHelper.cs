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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.BoxProvisioning.Tests.TestDoubles;

/// <summary>
/// Minimal <see cref="IAmAVersionDetectingMigrationHelper{TConnection,TTransaction}"/>
/// stub used to satisfy the <c>RelationalBoxMigrationRunnerBase</c> ctor. Members
/// throw <see cref="NotSupportedException"/> by default; tests that exercise the
/// base's default <c>RedetectStateAsync</c> set <see cref="TableExistsResult"/> and
/// <see cref="HistoryExistsResult"/> to drive <see cref="DoesTableExistAsync"/> and
/// <see cref="DoesHistoryExistAsync"/> instead.
/// </summary>
internal sealed class StubBoxDetectionHelper : IAmAVersionDetectingMigrationHelper<FakeDbConnection, FakeDbTransaction>
{
    public bool TableExistsResult { get; set; }
    public bool HistoryExistsResult { get; set; }
    public int DoesTableExistCallCount { get; private set; }
    public int DoesHistoryExistCallCount { get; private set; }

    public Task<bool> DoesTableExistAsync(
        FakeDbConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        FakeDbTransaction? transaction = null)
    {
        DoesTableExistCallCount++;
        return Task.FromResult(TableExistsResult);
    }

    public Task<bool> DoesHistoryExistAsync(
        FakeDbConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        FakeDbTransaction? transaction = null)
    {
        DoesHistoryExistCallCount++;
        return Task.FromResult(HistoryExistsResult);
    }

    public Task<int> GetMaxVersionAsync(
        FakeDbConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        FakeDbTransaction? transaction = null)
        => throw new NotSupportedException();

    public Task<IReadOnlyCollection<string>> GetTableColumnsAsync(
        FakeDbConnection connection, string tableName, string? schemaName,
        CancellationToken cancellationToken = default,
        FakeDbTransaction? transaction = null)
        => throw new NotSupportedException();

    public string DiscriminatorFor(BoxType boxType)
        => throw new NotSupportedException();

    public Task<int> DetectCurrentVersionAsync(
        FakeDbConnection connection, string tableName, string? schemaName,
        BoxType boxType, IReadOnlyList<IAmABoxMigration> migrations,
        CancellationToken cancellationToken = default,
        FakeDbTransaction? transaction = null)
        => throw new NotSupportedException();
}
