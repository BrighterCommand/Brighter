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
using Azure.Messaging.ServiceBus;

namespace Paramore.Brighter.AzureServiceBus.Tests.TestDoubles;

public class InMemoryRetryServiceBusClient(ServiceBusReceivedMessage message) : InMemoryServiceBusClient(message)
{
    public InMemoryRetryServiceBusReceiver Receiver { get; } = new(message);
    public List<(string Queue, ServiceBusMessage Message)> Scheduled { get; } = [];
    public List<InMemoryRetryServiceBusSender> Senders { get; } = [];
    public Exception? ScheduleFailure { get; set; }

    public override ServiceBusSender CreateSender(string queueOrTopicName, ServiceBusSenderOptions? options = default)
    {
        var sender = new InMemoryRetryServiceBusSender(this, queueOrTopicName);
        Senders.Add(sender);
        return sender;
    }

    public override ServiceBusReceiver CreateReceiver(string queueName, ServiceBusReceiverOptions options) => Receiver;
}
