#region Licence
/* The MIT License (MIT)
Copyright © 2015 Toby Henderson <hendersont@gmail.com>

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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public class InMemoryProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync, IAmABulkMessageProducerAsync
    {
        public Publication Publication { get; set; } = new();
        public readonly List<Message> SentMessages = new();
        public bool MessageWasSent { get; set; }
        public event Action<bool, string> OnMessagePublished;

        public void Dispose() { }

        public Task SendAsync(Message message)
        {
            var tcs = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
            Send(message);
            tcs.SetResult(message);
            return tcs.Task;
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<string[]> SendAsync(IEnumerable<Message> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var msgs = messages as Message[] ?? messages.ToArray();
            foreach (var msg in msgs)
            {
                OnMessagePublished?.Invoke(true, msg.Id); 
                yield return new[] { msg.Id };
            }
            MessageWasSent = true;
            SentMessages.AddRange(msgs);
        }

        public void Send(Message message)
        {
            MessageWasSent = true;
            SentMessages.Add(message);
            OnMessagePublished?.Invoke(true, message.Id);
        }

        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            Send(message);
        }
    }
}
