#region Licence
/* The MIT License (MIT)
Copyright © 2026 Guilherme Appel <guilhermeaappel@gmail.com>

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

using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Sqlite.Tests.Outbox;

/// <summary>
/// A <see cref="SqliteConnectionProvider"/> that counts the connections it hands out for asynchronous calls, so a
/// test can see how many asynchronous reads reached the database.
/// </summary>
public sealed class CountingSqliteConnectionProvider(IAmARelationalDatabaseConfiguration configuration)
    : SqliteConnectionProvider(configuration)
{
    private int _asyncConnections;

    /// <summary>
    /// How many connections this provider has handed out for asynchronous calls.
    /// </summary>
    public int AsyncConnections => Volatile.Read(ref _asyncConnections);

    /// <inheritdoc />
    public override Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _asyncConnections);
        return base.GetConnectionAsync(cancellationToken);
    }
}
