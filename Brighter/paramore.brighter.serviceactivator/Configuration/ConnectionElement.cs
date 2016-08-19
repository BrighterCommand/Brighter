#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

namespace paramore.brighter.serviceactivator.Configuration
{
    /// <summary>
    /// A connection to a broker, for reading messages from a queue
    /// </summary>
    public class ConnectionElement 
    {
        public ConnectionElement()
        {
            //We can use defaults for these, but other values must be set
            IsDurable = false;
            NoOfPerformers = 1;
            TimeoutInMiliseconds = 200;
            RequeueCount = -1;
            RequeueDelayInMilliseconds = 0;
            UnacceptableMessageLimit = 0;
            IsAsync = false;
        }

        /// <summary>
        /// The name of the connection, for identification
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// The name of the channel we use to talk to the broker (i.e. queue name)
        /// </summary>
        public string ChannelName { get; set; }

        /// <summary>
        /// The key we use to subscribe to messages on the broker to be placed onto our channel
        /// </summary>
        public string RoutingKey { get; set; }

        /// <summary>
        /// Does the queue store messages? Not needed if the sender can just resend missing messages.
        /// </summary>
        public bool IsDurable { get; set; }

        /// <summary>
        /// What type of message goes over the channel. Note that we take a DataType channel approach <see cref="http://www.enterpriseintegrationpatterns.com/patterns/messaging/DatatypeChannel.html"/>
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// How many thread should process messages on this queue. Allows us to provide Competing Consumers in-process <see cref="http://www.enterpriseintegrationpatterns.com/patterns/messaging/CompetingConsumers.html"/>
        /// </summary>
        public int NoOfPerformers { get; set; }

        /// <summary>
        /// How long before we timeout trying to read a message from the queue
        /// </summary>
        public int TimeoutInMiliseconds { get; set; }

        /// <summary>
        /// How often should we requeue a message before we 'fail' it
        /// </summary>
        public int RequeueCount { get; set; }

        /// <summary>
        /// How long should we wait before requeing a message
        /// </summary>
        public int RequeueDelayInMilliseconds { get; set; }

        /// <summary>
        /// How many messages should we allow to fail before we shut down the peformer reading messages
        /// </summary>
        public int UnacceptableMessageLimit { get; set; }

        /// <summary>
        /// Do we have an async pipeline to process messages
        /// </summary>
        public bool IsAsync { get; set; }
    }
}