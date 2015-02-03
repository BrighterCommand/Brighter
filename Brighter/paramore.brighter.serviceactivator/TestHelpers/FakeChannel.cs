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

using System.Collections.Concurrent;
using paramore.brighter.commandprocessor;

namespace paramore.brighter.serviceactivator.TestHelpers
{
    public class FakeChannel : IAmAnInputChannel, IAmAnOutputChannel
    {
        private readonly ConcurrentQueue<Message> messageQueue = new ConcurrentQueue<Message>();
        private readonly ChannelName channelName;
        public bool DisposeHappened { get; set; }

        public FakeChannel(string channelName = "", string routingKey = "")
        {
            this.channelName = new ChannelName(channelName);
        }

        public int Length
        {
            get { return messageQueue.Count; }
        }

        public ChannelName Name
        {
            get { return channelName; }
        }

        public void Acknowledge(Message message)
        {}

        public Message Receive(int timeoutinMilliseconds)
        {
            Message message;
            if (messageQueue.TryDequeue(out message))
                return message;
            else
                return new Message();
        }

        public void Reject(Message message)
        {}

        public void Stop()
        {
            messageQueue.Enqueue(MessageFactory.CreateQuitMessage());
        }

        public void Requeue(Message message)
        {
            messageQueue.Enqueue(message);
        }

        public void Send(Message message)
        {
            messageQueue.Enqueue(message);
        }

        public void Dispose()
        {
            DisposeHappened = true;
        }
    }
}