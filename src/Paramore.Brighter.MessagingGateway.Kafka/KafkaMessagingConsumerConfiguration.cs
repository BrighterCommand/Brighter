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
    public class KafkaMessagingConsumerConfiguration
    {
        /// <summary>
        /// What do we do if there is no offset stored in ZooKeeper for this consumer
        /// AutoOffsetReset.Earlist -  (our default) Begin reading the stream from the start
        /// AutoOffsetReset.Latest - Start from now i.e. only consume messages after we start
        /// AutoOffsetReset.Error - Consider it an error to be lacking a reset
        /// We use a default of earliest because we would rather duplicate messages than miss them due to an error
        /// or when you start up a new consumer.
        /// EnableAutoCommit - Allow the consumer to commit its offset based on a time interval insetead of after every message. 
        /// Enabling this lowers the load on the Kafka cluster but  you may process messages more than once if a consumer dies before commiting it's offset.
        /// AutoCommitIntervalMs - The time between committing offsets if AutoCommit is enabled
        /// </summary>
        public AutoOffsetReset OffsetDefault{ get; set; }
        public bool EnableAutoCommit { get; set; }
        public int AutoCommitIntervalMs { get; set; }

        public KafkaMessagingConsumerConfiguration()
        {
            OffsetDefault = AutoOffsetReset.Earliest;
            EnableAutoCommit = false;
            AutoCommitIntervalMs = 0;
        }
    }
}
