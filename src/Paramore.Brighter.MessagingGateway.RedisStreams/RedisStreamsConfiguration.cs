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

        /// <summary>
        /// How long to wait to retry between reconnection attempts
        /// </summary>
        public int ReconnectIntervalMs { get; set; } = 5000;

        /// <summary>
        /// Should reconnection attempts be linear or exponential
        /// </summary>
        public ReconnectStrategy ReconnectStrategy { get; set; } = ReconnectStrategy.Linear;
        
        // In general, let StackExchange.Redis handle most reconnects, but allow forced reconnect on errors 
        public int ReconnectMinFrequencyInSeconds { get; set; } = 60;

        // if errors continue for longer than the below threshold, then the multiplexer seems to not be reconnecting, so re-create the multiplexer
        public int ReconnectErrorThresholdInSeconds { get; set; } = 30;

        /// <summary>
        /// Should we use Twemproxy to talk to multiple instances
        /// </summary>
        public bool UseProxy { get; set; } = false;
    }
}
