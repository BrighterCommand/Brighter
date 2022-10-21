#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;
using Paramore.Brighter.MsSql;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync
    {
        /// <summary>
        /// How many outstanding messages may the outbox have before we terminate the programme with an OutboxLimitReached exception?
        /// -1 => No limit, although the Outbox may discard older entries which is implementation dependent
        /// 0 => No outstanding messages, i.e. throw an error as soon as something goes into the Outbox
        /// 1+ => Allow this number of messages to stack up in an Outbox before throwing an exception (likely to fail fast)
        /// </summary>
        public int MaxOutStandingMessages { get; set; } = -1;
  
        /// <summary>
        /// At what interval should we check the number of outstanding messages has not exceeded the limit set in MaxOutStandingMessages
        /// We spin off a thread to check when inserting an item into the outbox, if the interval since the last insertion is greater than this threshold
        /// If you set MaxOutStandingMessages to -1 or 0 this property is effectively ignored
        /// </summary>
        public int MaxOutStandingCheckIntervalMilliSeconds { get; set; } = 0;

        /// <summary>
        /// An outbox may require additional arguments before it can run its checks. The DynamoDb outbox for example expects there to be a Topic in the args
        /// This bag provides the args required
        /// </summary>
        public Dictionary<string, object> OutBoxBag { get; set; } = new Dictionary<string, object>();

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageProducer>();
        private readonly MsSqlMessageQueue<Message> _sqlQ;
        private Publication _publication; // -- placeholder for future use

        public MsSqlMessageProducer(
            MsSqlConfiguration msSqlConfiguration,
            IMsSqlConnectionProvider connectionProvider,
        Publication publication = null)
        {
            _sqlQ = new MsSqlMessageQueue<Message>(msSqlConfiguration, connectionProvider);
            _publication = publication ?? new Publication() {MakeChannels = OnMissingChannel.Create};
            MaxOutStandingMessages = _publication.MaxOutStandingMessages;
            MaxOutStandingCheckIntervalMilliSeconds = _publication.MaxOutStandingCheckIntervalMilliSeconds;
            OutBoxBag = publication.OutBoxBag;
        }

        public MsSqlMessageProducer(
            MsSqlConfiguration msSqlConfiguration,
            Publication publication = null) : this(msSqlConfiguration, new MsSqlSqlAuthConnectionProvider(msSqlConfiguration), publication)
        {
        }

        public void Send(Message message)
        {
            var topic = message.Header.Topic;

            s_logger.LogDebug("MsSqlMessageProducer: send message with topic {Topic} and id {Id}", topic,
                message.Id.ToString());

            _sqlQ.Send(message, topic);
        }
        
        public void SendWithDelay(Message message, int delayMilliseconds = 0)
        {
            //No delay support implemented
            Send(message);
        }
   

        public async Task SendAsync(Message message)
        {
            var topic = message.Header.Topic;

            s_logger.LogDebug(
                "MsSqlMessageProducer: send async message with topic {Topic} and id {Id}", topic,
                message.Id.ToString());

            await _sqlQ.SendAsync(message, topic);
        }

        public void Dispose()
        {
        }
    }
}
