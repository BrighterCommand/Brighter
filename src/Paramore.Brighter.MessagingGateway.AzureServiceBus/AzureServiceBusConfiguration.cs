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

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus;

/// <summary>
/// Configuration Options for the Azure Service Bus Messaging Transport.
/// </summary>
public class AzureServiceBusConfiguration
{
    /// <summary>
    /// Initializes an Instance of <see cref="AzureServiceBusConfiguration"/>
    /// </summary>
    /// <param name="connectionString">The Connection String to connect to Azure Service Bus.</param>
    /// <param name="ackOnRead">If True Messages and Read a Deleted, if False Messages are Peeked and Locked.</param>
    /// <param name="bulkSendBatchSize">When sending more than one message using the MessageProducer, the max amount to send in a single transmission.</param>
    public AzureServiceBusConfiguration(string connectionString, bool ackOnRead = false, int bulkSendBatchSize = 10 )
    {
        ConnectionString = connectionString;
        AckOnRead = ackOnRead;
        BulkSendBatchSize = bulkSendBatchSize;
    }

    /// <summary>
    /// The Connection String to connect to Azure Service Bus.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>
    /// When set to true this will set the Channel to Read and Delete, when False Peek and Lock
    /// </summary>
    public bool AckOnRead{ get; }

    /// <summary>
    /// When sending more than one message using the MessageProducer, the max amount to send in a single transmission.
    /// </summary>
    public int BulkSendBatchSize { get; }
}
