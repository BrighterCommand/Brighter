// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 09-27-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Repositories
{
    /// <summary>
    /// Class InMemoryRepository.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class InMemoryRepository<T> : IAmARepository<T> where T : class, IAmAnAggregate
    {
        private readonly ConcurrentDictionary<Identity, T> _domains = new ConcurrentDictionary<Identity, T>();
        private readonly ILog _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public InMemoryRepository(ILog logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Adds the specified aggregate.
        /// </summary>
        /// <param name="aggregate">The aggregate.</param>
        public void Add(T aggregate)
        {
            var op = new AddOperation(_domains, aggregate, _logger);
            var tx = Transaction.Current;
            if (tx == null)
            {
                op.OnCommit();
            }
            else
            {
                tx.EnlistVolatile(op, EnlistmentOptions.None);
            }
        }

        /// <summary>
        /// Gets the <see cref="T"/> at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>T.</returns>
        public T this[Identity index]
        {
            get
            {
                var aggregate = _domains.ContainsKey(index) ? _domains[index] : null;

                if (aggregate != null)
                {
                    var tx = Transaction.Current;
                    if (tx != null)
                    {
                        var op = new GetOperation(_domains, aggregate, _logger);
                        tx.EnlistVolatile(op, EnlistmentOptions.None);
                    }
                }

                return aggregate;
            }
        }

        /// <summary>
        /// Removes the specified identity.
        /// </summary>
        /// <param name="identity">The identity.</param>
        public void Remove(Identity identity)
        {
            var op = new RemoveOperation(_domains, identity, _logger);
            var tx = Transaction.Current;
            if (tx == null)
            {
                op.OnCommit();
            }
            else
            {
                tx.EnlistVolatile(op, EnlistmentOptions.None);
            }
        }

        public IEnumerable<T> Find(Func<T, bool> query)
        {
            return _domains.Where((pair) => query(pair.Value)).Select((pair) => pair.Value);
        }

        private abstract class RepositoryOperation : IEnlistmentNotification
        {
            protected readonly ConcurrentDictionary<Identity, T> Domains;
            protected T Aggregate;
            private readonly ILog _logger;

            protected RepositoryOperation(ConcurrentDictionary<Identity, T> domains, T aggregate, ILog logger)
            {
                this.Domains = domains;
                this.Aggregate = aggregate;
                _logger = logger;
            }

            public void Prepare(PreparingEnlistment preparingEnlistment)
            {
                _logger.DebugFormat("In Memory Repository, prepare notification received");
                OnPrepare();
                preparingEnlistment.Prepared();
            }

            internal virtual void OnPrepare() { }

            public void Commit(Enlistment enlistment)
            {
                _logger.DebugFormat("In Memory Repository, commit notification received");
                OnCommit();
                enlistment.Done();
            }

            internal virtual void OnCommit() { }


            public void Rollback(Enlistment enlistment)
            {
                _logger.DebugFormat("In Memory Repository, rollback notification received");
                OnRollback();
                enlistment.Done();
            }

            internal virtual void OnRollback() { }

            public void InDoubt(Enlistment enlistment)
            {
                _logger.DebugFormat("In Memory Repository, In doubt notification received");
                OnInDoubt();
                enlistment.Done();
            }

            internal virtual void OnInDoubt() { }
        }

        private class AddOperation : RepositoryOperation
        {
            public AddOperation(ConcurrentDictionary<Identity, T> domains, T aggregate, ILog logger)
                : base(domains, aggregate, logger)
            { }

            internal override void OnCommit()
            {
                Domains[Aggregate.Id] = Aggregate;
            }

            internal override void OnRollback()
            {
                T value;
                if (Domains.TryGetValue(Aggregate.Id, out value))
                {
                    Domains.TryRemove(Aggregate.Id, out value);
                }
            }
        }

        private class GetOperation : RepositoryOperation
        {
            public GetOperation(ConcurrentDictionary<Identity, T> domains, T aggregate, ILog logger)
                : base(domains, aggregate, logger)
            { }

            internal override void OnRollback()
            {
                //swap the old value back, in case we modified inside a transaction
                Domains[Aggregate.Id] = Aggregate;
            }
        }

        private class RemoveOperation : RepositoryOperation
        {
            private readonly Identity _identity;

            public RemoveOperation(ConcurrentDictionary<Identity, T> domains, Identity identity, ILog logger)
                : base(domains, null, logger)
            {
                _identity = identity;
            }

            internal override void OnCommit()
            {
                Domains.TryRemove(_identity, out Aggregate);
            }

            internal override void OnRollback()
            {
                //we may have already rolled back via Get unwind
                if (!Domains.ContainsKey(_identity))
                {
                    Domains[_identity] = Aggregate;
                }
            }
        }
    }
}