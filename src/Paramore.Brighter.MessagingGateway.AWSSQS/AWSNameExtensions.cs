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

namespace Paramore.Brighter.MessagingGateway.AWSSQS
{
    public static class AWSNameExtensions
    {
        public static ChannelName ToValidSQSQueueName(this ChannelName? channelName, bool isFifo = false)
        {
            if (channelName is null)
                return new ChannelName(string.Empty);
            
            //SQS only allows 80 characters alphanumeric, hyphens, and underscores, but we might use a period in a 
            //default typename strategy
            var name = channelName.Value;
            name = name.Replace(".", "_");
            if (name.Length > 80)
                name = name.Substring(0, 80);

            if (isFifo)
            {
                name = name + ".fifo";
            }

            return new ChannelName(name);
        }

        public static RoutingKey ToValidSNSTopicName(this RoutingKey routingKey)
        {
            //SNS only topic names are limited to 256 characters. Alphanumeric characters plus hyphens (-) and
            //underscores (_) are allowed. Topic names must be unique within an AWS account.
            var topic = routingKey.Value;
            topic = topic.Replace(".", "_");
            if (topic.Length > 256)
                topic = topic.Substring(0, 256);
            
            return new RoutingKey(topic);
        }
        
        public static string ToValidSNSTopicName(this string topic)
        {
            //SNS only topic names are limited to 256 characters. Alphanumeric characters plus hyphens (-) and
            //underscores (_) are allowed. Topic names must be unique within an AWS account.
            topic = topic.Replace(".", "_");
            if (topic.Length > 256)
                topic = topic.Substring(0, 256);
            
            return topic;
        }
     }
}
