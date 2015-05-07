using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.SQS;
using Amazon.SQS.Model;

namespace paramore.brighter.commandprocessor.messaginggateway.awssqs
{
    public sealed class SqsQueuedRetriever
    {
        private static readonly Lazy<SqsQueuedRetriever> Lazy =
            new Lazy<SqsQueuedRetriever>(() => new SqsQueuedRetriever());

        private readonly ConcurrentDictionary<string, ConcurrentQueue<Amazon.SQS.Model.Message>> _queue;
        private readonly object _queueLock;

        public static SqsQueuedRetriever Instance { get { return Lazy.Value; } }

        private SqsQueuedRetriever()
        {
             _queueLock = new object();
            _queue = new ConcurrentDictionary<string, ConcurrentQueue<Amazon.SQS.Model.Message>>();
        }

        public async Task<Amazon.SQS.Model.Message> GetMessage(string queueName, int  timeoutInMilliseconds, int numberOfCacheableMessages)
        {
            Amazon.SQS.Model.Message message = null;
            if (_queue.ContainsKey(queueName) && _queue[queueName].TryDequeue(out message)) return message;
            
            var request = new ReceiveMessageRequest(queueName)
            {
                MaxNumberOfMessages = numberOfCacheableMessages,
                WaitTimeSeconds = (int)TimeSpan.FromMilliseconds(timeoutInMilliseconds).TotalSeconds
            };

            using (var client = new AmazonSQSClient())
            {
                var response = await client.ReceiveMessageAsync(request);
                                
                if (response.HttpStatusCode != HttpStatusCode.OK) return message;

                if (response.ContentLength == 0) return message;

                if (!response.Messages.Any()) return message;

                AddToQueue(queueName, response.Messages);

                if(_queue.ContainsKey(queueName))
                    _queue[queueName].TryDequeue(out message);
            }

            return message;
        }
        
        private void AddToQueue(string queueName, List<Amazon.SQS.Model.Message> messages)
        {
            lock (_queueLock)
            {
                var messageQueue = new ConcurrentQueue<Amazon.SQS.Model.Message>();
                if (!_queue.ContainsKey(queueName))
                    _queue.TryAdd(queueName, messageQueue);

                messages.ForEach(x => _queue[queueName].Enqueue(x));
            }
        }
    }
}