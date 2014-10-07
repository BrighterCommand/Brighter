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
using System.Transactions;
using Common.Logging;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Repositories
{
    /// <summary>
    /// Class InMemoryRepository.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class InMemoryRepository<T> : IAmARepository<T> where T: class, IAmAnAggregate
    {
        readonly ConcurrentDictionary<Identity, T> domains = new  ConcurrentDictionary<Identity, T>();
        readonly ILog logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public InMemoryRepository(ILog logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Adds the specified aggregate.
        /// </summary>
        /// <param name="aggregate">The aggregate.</param>
        public void Add(T aggregate)
        {
            var op = new AddOperation(domains, aggregate, logger);
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
                if (domains.ContainsKey(index))
                    return domains[index];
                else
                    return null;
            }
        }

        abstract class RepositoryOperation : IEnlistmentNotification
        {
            protected readonly ConcurrentDictionary<Identity, T> Domains;
            protected readonly T Aggregate;
            protected readonly ILog Logger;

            protected RepositoryOperation(ConcurrentDictionary<Identity, T> domains, T aggregate, ILog logger)
            {
                this.Domains = domains;
                this.Aggregate = aggregate;
                this.Logger = logger;
            }

            public void Prepare(PreparingEnlistment preparingEnlistment)
            {
                Logger.DebugFormat("In Memory Repository, prepare notification received");
                preparingEnlistment.Prepared();
            }

            public void Commit(Enlistment enlistment)
            {
                Logger.DebugFormat("In Memory Repository, commit notification received");
                OnCommit();
                enlistment.Done();
            }

            internal virtual void OnCommit() {}
            

            public void Rollback(Enlistment enlistment)
            {
                Logger.DebugFormat("In Memory Repository, rollback notification received");
                OnRollback();
                enlistment.Done();
            }

            internal virtual void OnRollback(){}

            public void InDoubt(Enlistment enlistment)
            {
                Logger.DebugFormat("In Memory Repository, In doubt notification received");
                OnInDoubt();
                enlistment.Done();
            }

            internal virtual void OnInDoubt(){}
        }

        class AddOperation : RepositoryOperation
        {
            public AddOperation(ConcurrentDictionary<Identity, T> domains, T aggregate, ILog logger) 
                : base(domains, aggregate, logger)
            {}

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
        
    }
}