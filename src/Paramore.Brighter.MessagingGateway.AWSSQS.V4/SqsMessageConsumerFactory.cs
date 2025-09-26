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
/// Class SqsMessageConsumerFactory.
/// </summary>
public class SqsMessageConsumerFactory : IAmAMessageConsumerFactory
{
    private readonly AWSMessagingGatewayConnection _awsConnection;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqsMessageConsumerFactory"/> class.
    /// </summary>
    public SqsMessageConsumerFactory(AWSMessagingGatewayConnection awsConnection)
    {
        _awsConnection = awsConnection;
    }

    /// <summary>
    /// Creates a consumer for the specified queue.
    /// </summary>
    /// <param name="subscription">The queue to connect to</param>
    /// <returns>IAmAMessageConsumerSync.</returns>
    public IAmAMessageConsumerSync Create(Subscription subscription)
    {
        return CreateImpl(subscription);
    }

    public IAmAMessageConsumerAsync CreateAsync(Subscription subscription)
    {
        return CreateImpl(subscription);
    }


    private SqsMessageConsumer CreateImpl(Subscription subscription)
    {
        SqsSubscription? sqsSubscription = subscription as SqsSubscription;
        if (sqsSubscription == null) throw new ConfigurationException("We expect an SqsSubscription or SqsSubscription<T> as a parameter");

        //if it is a url, don't alter; if it is just a name, ensure it is valid
        ChannelName queueName = subscription.ChannelName;
        if (sqsSubscription.FindQueueBy == QueueFindBy.Name)    
            queueName =queueName.ToValidSQSQueueName(sqsSubscription.QueueAttributes.Type == SqsType.Fifo);
            
        return new SqsMessageConsumer(
            awsConnection: _awsConnection, 
            queueName: queueName, 
            isQueueUrl: (sqsSubscription.FindQueueBy == QueueFindBy.Url),   
            batchSize: subscription.BufferSize,
            hasDlq: sqsSubscription.QueueAttributes.RedrivePolicy == null,
            rawMessageDelivery: sqsSubscription.QueueAttributes.RawMessageDelivery
        );
    }
}