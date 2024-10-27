using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

//Based on https://devblogs.microsoft.com/pfxteam/await-synchronizationcontext-and-console-apps/

namespace Paramore.Brighter.ServiceActivator
{
    internal class BrighterSynchronizationContext : SynchronizationContext
    {
       private readonly BlockingCollection<KeyValuePair<SendOrPostCallback, object?>> _queue = new();
       private int _operationCount;
       
       /// <summary>
       /// When we have completed the operations, we can exit
       /// </summary>
       public override void OperationCompleted()
       {
           if (Interlocked.Decrement(ref _operationCount) == 0)
               Complete();
       }
       
       /// <summary>
       /// Tracks the number of ongoing operations, so we know when 'done'
       /// </summary>
       public override void OperationStarted()
       {
           Interlocked.Increment(ref _operationCount);
       }

        /// <summary>Dispatches an asynchronous message to the synchronization context.</summary>
        /// <param name="d">The System.Threading.SendOrPostCallback delegate to call.</param>
        /// <param name="state">The object passed to the delegate.</param>
        public override void Post(SendOrPostCallback d, object? state)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            _queue.Add(new KeyValuePair<SendOrPostCallback, object?>(d, state));
        }

        /// <summary>Not supported.</summary>
        public override void Send(SendOrPostCallback d, object? state)
        {
            throw new NotSupportedException("Synchronously sending is not supported.");
        }

        /// <summary>Runs a loop to process all queued work items.</summary>
        public void RunOnCurrentThread()
        {
            foreach (var workItem in _queue.GetConsumingEnumerable())
                workItem.Key(workItem.Value);
        }

        /// <summary>Notifies the context that no more work will arrive.</summary>
        private void Complete() { _queue.CompleteAdding(); }
    }
}
