#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Paramore.Brighter
{
    /// <summary>
    /// Provides a committable transaction for use in a box transaction.
    /// </summary>
    /// <remarks>
    /// This class implements the IAmABoxTransactionProvider interface for CommittableTransaction.
    /// It manages the creation, commitment, and rollback of transactions, as well as handling
    /// the current transaction context.
    /// </remarks>
    public class InMemoryTransactionProvider : IAmABoxTransactionProvider<CommittableTransaction>
    {
        private CommittableTransaction? _transaction;
        private Transaction? _existingTransaction;

        /// <summary>
        /// Closes the current transaction and restores the previous transaction context.
        /// </summary>
        public void Close()
        {
            Transaction.Current = _existingTransaction;
            _transaction = null;
        }

        /// <summary>
        /// Commits the current transaction and closes it.
        /// </summary>
        public void Commit()
        {
            _transaction?.Commit();
            Close();
        }

        /// <summary>
        /// Asynchronously commits the current transaction.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the commit operation.</param>
        /// <returns>A task representing the asynchronous commit operation.</returns>
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            if (_transaction is null)
                return Task.CompletedTask;
            return Task.Factory.FromAsync(_transaction.BeginCommit, _transaction.EndCommit, null, TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>
        /// Gets the current transaction, creating a new one if necessary.
        /// </summary>
        /// <returns>The current CommittableTransaction.</returns>
        public CommittableTransaction GetTransaction()
        {
            if (_transaction == null)
            {
                _existingTransaction = Transaction.Current;
                _transaction = new CommittableTransaction();
                Transaction.Current = _transaction;
            }
            return _transaction;
        }

        /// <summary>
        /// Asynchronously gets the current transaction, creating a new one if necessary.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation, which resolves to the current CommittableTransaction.</returns>
        public Task<CommittableTransaction> GetTransactionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<CommittableTransaction>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(GetTransaction());
            return tcs.Task;
        }

        /// <summary>
        /// Gets a value indicating whether there is an open transaction.
        /// </summary>
        public bool HasOpenTransaction { get { return _transaction != null; } }

        /// <summary>
        /// Gets a value indicating whether this provider uses a shared connection.
        /// </summary>        
        public bool IsSharedConnection => true;
        
        /// <summary>
        /// Rolls back the current transaction and closes it.
        /// </summary>
        public void Rollback()
        {
            _transaction?.Rollback();
            Close();
        }

        /// <summary>
        /// Asynchronously rolls back the current transaction.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the rollback operation.</param>
        /// <returns>A task representing the asynchronous rollback operation.</returns>
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Rollback();
            return Task.CompletedTask;
        }
    }
}
