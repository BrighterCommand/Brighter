#region Licence
/* The MIT License (MIT)
Copyright © 2017 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

namespace Paramore.Brighter.MessagingGateway.RedisStreams
{
    public enum ReconnectStrategy
    {
        Linear,
        Exponential
    }
    
    public class RedisStreamsConfiguration
    {
        /// <summary>
        /// See https://stackexchange.github.io/StackExchange.Redis/Configuration
        /// </summary>
        public string ConfigurationOptions { get; set; }

        //How may messages to read at once from the stream
        public int BatchSize { get; set; } = 10;

        /// <summary>
        /// If this is a consumer, what group does it belong to?
        /// Within a group of consumers, they will employ a lock and read-past strategy. When a message is locked by a specific consumer, other consumers in the
        /// group will read-past in the stream to get subsequent message. This allows us to use the competing consumers pattern with Redis.
        /// Every consumer you want to be part of the same group, must have the same consumer group.
        /// Because we use the group syntax to read, this will default to a unique id i.e. each consumer is a group, unless set
        /// </summary>
        public string ConsumerGroup { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// What is the id of this consumer = used for tracking messages read in a group
        /// </summary>
        public string ConsumerId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// How long to wait to retry between reconnection attempts
        /// </summary>
        public int ReconnectIntervalMs { get; set; } = 5000;

        /// <summary>
        /// Should reconnection attempts be linear or exponential
        /// </summary>
        public ReconnectStrategy ReconnectStrategy { get; set; } = ReconnectStrategy.Linear;
        
        /// <summary>
        /// In general, let StackExchange.Redis handle most reconnects, but allow forced reconnect on errors 
        /// </summary>
        public int ReconnectMinFrequencyInSeconds { get; set; } = 60;

        /// <summary>
        /// if errors continue for longer than the below threshold, then the multiplexer seems to not be reconnecting, so re-create the multiplexer
        /// </summary>
        public int ReconnectErrorThresholdInSeconds { get; set; } = 30;

        /// <summary>
        /// Should we use Twemproxy to talk to multiple instances
        /// </summary>
        public bool UseProxy { get; set; } = false;
    }
}
