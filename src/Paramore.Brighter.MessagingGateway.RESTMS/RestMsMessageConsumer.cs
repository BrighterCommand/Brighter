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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Paramore.Brighter.MessagingGateway.RESTMS.Exceptions;
using Paramore.Brighter.MessagingGateway.RESTMS.Logging;
using Paramore.Brighter.MessagingGateway.RESTMS.MessagingGatewayConfiguration;
using Paramore.Brighter.MessagingGateway.RESTMS.Model;

namespace Paramore.Brighter.MessagingGateway.RESTMS
{
    /// <summary>
    /// Class RestMsMessageConsumer.
    /// </summary>
    public class RestMsMessageConsumer : RestMSMessageGateway, IAmAMessageConsumer
    {
        private static readonly Lazy<ILog> _logger = new Lazy<ILog>(LogProvider.For<RestMsMessageConsumer>);

        private readonly string _queueName;
        private readonly string _routingKey;
        private Pipe _pipe;
        private readonly Feed _feed;
        private readonly Domain _domain;

        /// <summary>
        /// Initializes a new instance of the <see cref="RestMsMessageConsumer"/> class.
        /// </summary>
        /// <param name="configuration">The configuration of the RestMS broker</param>
        /// <param name="queueName">Name of the queue.</param>
        /// <param name="routingKey">The routing key.</param>
        public RestMsMessageConsumer(RestMSMessagingGatewayConfiguration configuration, string queueName, string routingKey) 
            : base(configuration)
        {
            _queueName = queueName;
            _routingKey = routingKey;
            _feed = new Feed(this);
            _domain = new Domain(this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Receives the specified queue name.
        /// </summary>

        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>Message.</returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Message[] Receive(int timeoutInMilliseconds = -1)
        {
            try
            {
                _feed.EnsureFeedExists(_domain.GetDomain());
                _pipe = new Pipe(this, _feed);
                _pipe.EnsurePipeExists(_queueName, _routingKey, _domain.GetDomain());

                return ReadMessage();
            }
            catch (RestMSClientException rmse)
            {
                _logger.Value.ErrorFormat("Error sending to the RestMS server: {0}", rmse.ToString());
                throw;
            }
            catch (HttpRequestException he)
            {
                _logger.Value.ErrorFormat("HTTP error on request to the RestMS server: {0}", he.ToString());
                throw;
            }
        }

        /// <summary>
        /// Acknowledges the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Acknowledge(Message message)
        {
            var pipe = _pipe.GetPipe();
            DeleteMessage(pipe, message);
        }

        /// <summary>
        /// Noes the of outstanding messages.
        /// </summary>
        /// <param name="timeoutInMilliseconds">The timeout in milliseconds.</param>
        /// <returns>System.Int32.</returns>
        public int NoOfOutstandingMessages(int timeoutInMilliseconds = -1)
        {
            try
            {
                _pipe.EnsurePipeExists(_queueName, _routingKey, _domain.GetDomain());
                var pipe = _pipe.GetPipe();
                return pipe.Messages?.Length ?? 0;
            }
            catch (RestMSClientException rmse)
            {
                _logger.Value.ErrorFormat("Error sending to the RestMS server: {0}", rmse.ToString());
                throw;
            }
            catch (HttpRequestException he)
            {
                _logger.Value.ErrorFormat("HTTP error on request to the RestMS server: {0}", he.ToString());
                throw;
            }
        }

        /// <summary>
        /// Purges the specified queue name.
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Purge()
        {
            try
            {
                var pipe = _pipe.GetPipe();
                if (pipe?.Messages != null)
                {
                    var message = pipe.Messages.FirstOrDefault();
                    do
                    {
                        if (message != null)
                        {
                            SendDeleteMessage(message);
                        }
                        pipe = _pipe.GetPipe();
                    } while (pipe.Messages != null && pipe.Messages.Any());
                }
            }
            catch (RestMSClientException rmse)
            {
                _logger.Value.ErrorFormat("Error sending to the RestMS server: {0}", rmse.ToString());
                throw;
            }
            catch (HttpRequestException he)
            {
                _logger.Value.ErrorFormat("HTTP error on request to the RestMS server: {0}", he.ToString());
                throw;
            }
        }

        public void Requeue(Message message)
        {
        }

        /// <summary>
        /// Requeues the specified message. Not implemented
        /// </summary>
        /// <param name="message"></param>
        /// <param name="delayMilliseconds">Number of milliseconds to delay delivery of the message.</param>
        public void Requeue(Message message, int delayMilliseconds)
        {
            Task.Delay(delayMilliseconds);
            Requeue(message);
        }
 

        /// <summary>
        /// Rejects the specified message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="requeue">if set to <c>true</c> [requeue].</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Reject(Message message, bool requeue)
        {
        }


        private void DeleteMessage(RestMSPipe pipe, Message message)
        {
            if (pipe.Messages == null || !pipe.Messages.Any())
            {
                return;
            }

            var matchingMessage = pipe.Messages.FirstOrDefault(msg => msg.MessageId == message.Id.ToString());
            if (matchingMessage == null)
            {
                return;
            }

            _logger.Value.DebugFormat("Deleting the message {0} from the pipe: {0}", message.Id, pipe.Href);
            SendDeleteMessage(matchingMessage);
        }

        private Message[] GetMessage(RestMSMessageLink messageUri)
        {
            if (messageUri == null)
            {
                return new Message[] {new Message()};
            }

            _logger.Value.DebugFormat("Getting the message from the RestMS server: {0}", messageUri);
            var client = Client();

            try
            {
                var response = client.GetAsync(messageUri.Href).Result;
                response.EnsureSuccessStatusCode();
                var pipeMessage = ParseResponse<RestMSMessage>(response);
                return new Message[] {RestMSMessageCreator.CreateMessage(pipeMessage)};
            }
            catch (AggregateException ae)
            {
                foreach (var exception in ae.Flatten().InnerExceptions)
                {
                    _logger.Value.ErrorFormat("Threw exception getting Pipe {0} from RestMS Server {1}", _pipe.PipeUri, exception.Message);
                }

                throw new RestMSClientException(string.Format("Error retrieving the domain from the RestMS server, see log for details"));
            }
        }

        private Message[] ReadMessage()
        {
            var pipe = _pipe.GetPipe();
            return GetMessage(pipe.Messages?.FirstOrDefault());
        }

        private void SendDeleteMessage(RestMSMessageLink matchingMessage)
        {
            var client = Client();
            var response = client.DeleteAsync(matchingMessage.Href).Result;
            response.EnsureSuccessStatusCode();
        }
    }
}
