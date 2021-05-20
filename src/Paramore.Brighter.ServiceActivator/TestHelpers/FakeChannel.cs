﻿#region Licence
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

using System.Collections.Concurrent;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.ServiceActivator.TestHelpers
{
    public class FakeChannel : IAmAChannel
    {
        private ConcurrentQueue<Message> _messageQueue = new ConcurrentQueue<Message>();

        public bool DisposeHappened { get; set; }
        public bool AcknowledgeHappened { get; set; }
        public int AcknowledgeCount { get; set; }
        public int RejectCount { get; set; }
        public int RequeueCount { get; set; }
        public virtual ChannelName Name { get; }
        public virtual int Length => _messageQueue.Count;

        public FakeChannel(string channelName = "", string routingKey = "")
        {
            Name = new ChannelName(channelName);
        }

        public virtual void Acknowledge(Message message)
        {
            AcknowledgeHappened = true;
            AcknowledgeCount++;
        }

        public virtual void Dispose()
        {
            DisposeHappened = true;
        }
        
        public void Enqueue(params Message[] messages)
        {
            messages.Each((message) => _messageQueue.Enqueue(message));
        }

        public void Purge()
        {
            _messageQueue = new ConcurrentQueue<Message>();
        }

        public virtual Message Receive(int timeoutinMilliseconds)
        {
            return _messageQueue.TryDequeue(out Message message) ? message : new Message();
        }

        public virtual void Reject(Message message)
        {
            RejectCount++;
        }

        public virtual void Requeue(Message message, int delayMilliseconds = 0)
        {
            RequeueCount++;
            _messageQueue.Enqueue(message);
        }

        public virtual void Stop()
        {
            _messageQueue.Enqueue(MessageFactory.CreateQuitMessage());
        }

   }
}
