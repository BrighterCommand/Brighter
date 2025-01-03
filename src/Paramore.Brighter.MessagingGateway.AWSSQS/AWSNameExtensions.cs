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

namespace Paramore.Brighter.MessagingGateway.AWSSQS;

public static class AWSNameExtensions
{
    public static ChannelName ToValidSQSQueueName(this ChannelName? channelName, bool isFifo = false)
    {
        if (channelName is null)
        {
            return new ChannelName(string.Empty);
        }

        //SQS only allows 80 characters alphanumeric, hyphens, and underscores, but we might use a period in a 
        //default typename strategy
        var queue = channelName.Value;
        var maxLength = isFifo ? 75 : 80;

        queue = queue.Replace('.', '_');
        if (queue.Length > maxLength)
        {
            queue = queue.Substring(0, maxLength);
        }

        if (isFifo)
        {
            queue += ".fifo";
        }

        return new ChannelName(queue);
    }

    public static RoutingKey ToValidSNSTopicName(this RoutingKey routingKey, bool isFifo = false)
        => new(routingKey.Value.ToValidSNSTopicName(isFifo));

    public static string ToValidSNSTopicName(this string topic, bool isFifo = false)
    {
        //SNS only topic names are limited to 256 characters. Alphanumeric characters plus hyphens (-) and
        //underscores (_) are allowed. Topic names must be unique within an AWS account.
        var maxLength = isFifo ? 251 : 256;

        topic = topic.Replace('.', '_');
        if (topic.Length > maxLength)
        {
            topic = topic.Substring(0, maxLength);
        }

        if (isFifo)
        {
            topic += ".fifo";
        }

        return topic;
    }
}
