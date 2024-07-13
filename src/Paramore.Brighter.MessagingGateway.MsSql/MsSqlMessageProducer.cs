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
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.MessagingGateway.MsSql.SqlQueues;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.MessagingGateway.MsSql
{
    public class MsSqlMessageProducer : IAmAMessageProducerSync, IAmAMessageProducerAsync
    {
        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<MsSqlMessageProducer>();
        private readonly MsSqlMessageQueue<Message> _sqlQ;

        /// <summary>
        /// The Publication used to configure the producer
        /// </summary>
        public Publication Publication { get; }

        /// <summary>
        /// The OTel Span we are writing Producer events too
        /// </summary>
        public Activity Span { get; set; }

        public MsSqlMessageProducer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            IAmARelationalDbConnectionProvider connectonProvider,
            Publication publication = null
        )
        {
            _sqlQ = new MsSqlMessageQueue<Message>(msSqlConfiguration, connectonProvider);
            Publication = publication ?? new Publication {MakeChannels = OnMissingChannel.Create};
        }

        public MsSqlMessageProducer(
            RelationalDatabaseConfiguration msSqlConfiguration,
            Publication publication = null) : this(msSqlConfiguration, new MsSqlConnectionProvider(msSqlConfiguration), publication)
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
