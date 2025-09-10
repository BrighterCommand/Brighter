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

namespace Paramore.Brighter.MessagingGateway.AWSSQS.V4;

/// <summary>
/// Encapsulates the parameters for a redrive policy for an SQS queue
/// </summary>
public class RedrivePolicy
{
    /// <summary>
    /// The maximum number of requeues for a message before we push it to the DLQ instead 
    /// </summary>
    public int MaxReceiveCount { get; set; }
        
    /// <summary>
    /// The name of the dead letter queue we want to associate with any redrive policy
    /// </summary>
    public ChannelName DeadlLetterQueueName { get; set; }

    /// <summary>
    /// The policy that puts an upper limit on requeues before moving to a DLQ 
    /// </summary>
    /// <param name="deadLetterQueueName">The name of any dead letter queue used by a redrive policy</param>
    /// <param name="maxReceiveCount">The maximum number of retries before we push to a DLQ</param>
    public RedrivePolicy(ChannelName deadLetterQueueName, int maxReceiveCount)
    {
        MaxReceiveCount = maxReceiveCount;
        DeadlLetterQueueName = deadLetterQueueName;
    }
}