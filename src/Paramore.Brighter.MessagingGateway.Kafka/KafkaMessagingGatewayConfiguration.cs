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

using System.Collections.Generic;

namespace Paramore.Brighter.MessagingGateway.Kafka
{
    public class KafkaMessagingGatewayConfiguration
    {
        /// <summary>
        /// Client identifier.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Initial list of brokers as a list of 
        /// broker host or host:port. "
        /// </summary>
        public string[] BootStrapServers { get; set; }

        /// <summary>
        /// Maximum number of in-flight requests the 
        /// client will send. 
        /// This setting applies per broker connection.
        /// </summary>
        public int? MaxInFlightRequestsPerConnection { get; set; }

        //public IEnumerable<KeyValuePair<string, object>> ToConfig()
        //{
        //    var config = new Dictionary<string, object>()
        //    {
        //        {"client.id", Name },
        //        {"bootstrap.servers", string.Join(", ", BootStrapServers)}
        //    };

        //    if (MaxInFlightRequestsPerConnection.HasValue)
        //        config["max.in.flight.requests.per.connection"] = MaxInFlightRequestsPerConnection.Value;
        //    return config;
        //}
    }
}
