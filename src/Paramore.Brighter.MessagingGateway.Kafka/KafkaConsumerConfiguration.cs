#region Licence
/* The MIT License (MIT)
Copyright © 2015 Wayne Hunsley <whunsley@gmail.com>

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

using Confluent.Kafka;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class KafkaConsumerConfiguration
    {
        /// <summary>
        /// Only one consumer in a group can read from a partition at any one time; this preserves ordering
        /// We do not default this value, and expect you to set it
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// If Kafka does not receive a heartbeat from the consumer within this time window, trigger a re-balance
        /// Default is Kafka default of 10s
        /// </summary>
        public int? SessionTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// How often the consumer needs to poll for new messages to be considered alive, polling greater than this interval triggers a rebalance
        /// Uses Kafka default of 300000
        /// </summary>
        public int MaxPollIntervalMs { get; set; } = 300000;

        /// <summary>
        /// Default to read only committed messages, change if you want to read uncommited messages. May cause duplicates.
        /// </summary>
        public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;
        
        /// <summary>
        /// What do we do if there is no offset stored in ZooKeeper for this consumer
        /// AutoOffsetReset.Earlist -  (default) Begin reading the stream from the start
        /// AutoOffsetReset.Latest - Start from now i.e. only consume messages after we start
        /// AutoOffsetReset.Error - Consider it an error to be lacking a reset
        /// </summary>
        public AutoOffsetReset OffsetDefault { get; set; } = AutoOffsetReset.Earliest;

        /// <summary>
        /// We commit processed work (marked as acked or rejected) when a batch size worth of work has been completed
        /// If the batch size is 1, then there is a low risk of offsets not being committed and therefore duplicates appearing
        /// in the stream, but the latency per request and load on the broker increases. As the batch size rises the risk of
        /// a crashing worker process failing to commit a batch that is then represented rises.
        /// </summary>
        public long CommitBatchSize { get; set; } = 1;

    }
}
