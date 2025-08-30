#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper ian_hammond_cooper@yahoo.co.uk

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
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Paramore.Brighter;

/// <summary>
/// Provides an in-memory implementation of <see cref="IAmABoxTransactionProvider{T}"/> using <see cref="CommittableTransaction"/>.
/// Intended for scenarios where a lightweight, in-memory transaction is sufficient, such as testing or in-memory outbox.
/// </summary>
public class InMemoryTransactionProvider : IAmABoxTransactionProvider<CommittableTransaction>
{
    private CommittableTransaction? _transaction = null;
    private bool _committedOrRolledBack = false;

    /// <summary>
    /// Closes the current transaction and disposes resources.
    /// </summary>
    public void Close()
    {
        _transaction?.Dispose();
        _transaction = null;
        _committedOrRolledBack = true;
    }

    /// <summary>
    /// Commits the current transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if there is no open transaction to commit.</exception>
    public void Commit()
    {
        if (_transaction == null || _committedOrRolledBack)
            throw new InvalidOperationException("No open transaction to commit.");
        _transaction.Commit();
        _committedOrRolledBack = true;
    }

    /// <summary>
    /// Asynchronously commits the current transaction.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if there is no open transaction to commit.</exception>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null || _committedOrRolledBack)
            throw new InvalidOperationException("No open transaction to commit.");
        await Task.Run(() => _transaction.Commit(), cancellationToken);
        _committedOrRolledBack = true;
    }

    /// <summary>
    /// Indicates whether there is an open transaction.
    /// </summary>
    /// <value><c>true</c> if a transaction is open and not yet committed or rolled back; otherwise, <c>false</c>.</value>
    public bool HasOpenTransaction => _transaction != null && !_committedOrRolledBack;

    /// <summary>
    /// Indicates whether the transaction provider uses a shared connection.
    /// </summary>
    /// <value>Always <c>false</c> for this implementation.</value>
    public bool IsSharedConnection => false;

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if there is no open transaction to rollback.</exception>
    public void Rollback()
    {
        if (_transaction == null || _committedOrRolledBack)
            throw new InvalidOperationException("No open transaction to rollback.");
        _transaction.Rollback();
        _committedOrRolledBack = true;
    }

    /// <summary>
    /// Asynchronously rolls back the current transaction.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if there is no open transaction to rollback.</exception>
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null || _committedOrRolledBack)
            throw new InvalidOperationException("No open transaction to rollback.");
        await Task.Run(() => _transaction.Rollback(), cancellationToken);
        _committedOrRolledBack = true;
    }

    /// <summary>
    /// Gets the current <see cref="CommittableTransaction"/> instance.
    /// Creates a new transaction if none exists or if the previous transaction was committed or rolled back.
    /// </summary>
    /// <returns>The current <see cref="CommittableTransaction"/>.</returns>
    public CommittableTransaction GetTransaction()
    {
        if (_transaction == null || _committedOrRolledBack)
           _transaction = new CommittableTransaction();
        return _transaction;
    }

    /// <summary>
    /// Asynchronously gets the current <see cref="CommittableTransaction"/> instance.
    /// Creates a new transaction if none exists or if the previous transaction was committed or rolled back.
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
    /// <returns>A <see cref="Task{CommittableTransaction}"/> representing the asynchronous operation.</returns>
    public Task<CommittableTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null || _committedOrRolledBack)
            _transaction = new CommittableTransaction();
        return Task.FromResult(_transaction);
    }
}
