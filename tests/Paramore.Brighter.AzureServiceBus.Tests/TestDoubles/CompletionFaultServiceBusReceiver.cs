#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

public class CompletionFaultServiceBusReceiver(ServiceBusReceiver receiver) : ServiceBusReceiver
{
    public int CompletionAttempts { get; private set; }
    public override bool IsClosed => receiver.IsClosed;
    public override string EntityPath => receiver.EntityPath;

    public override Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(
        int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
        => receiver.ReceiveMessagesAsync(maxMessages, maxWaitTime, cancellationToken);

    public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        CompletionAttempts++;
        if (CompletionAttempts == 1)
            throw new ServiceBusException("Completion unavailable", ServiceBusFailureReason.ServiceBusy);
        return receiver.CompleteMessageAsync(message, cancellationToken);
    }

    public override Task AbandonMessageAsync(ServiceBusReceivedMessage message,
        IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
        => receiver.AbandonMessageAsync(message, propertiesToModify, cancellationToken);

    public override Task RenewMessageLockAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
        => receiver.RenewMessageLockAsync(message, cancellationToken);

    public override Task CloseAsync(CancellationToken cancellationToken = default) => receiver.CloseAsync(cancellationToken);
}
