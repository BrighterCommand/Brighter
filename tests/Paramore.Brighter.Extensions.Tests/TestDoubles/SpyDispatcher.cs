#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System.Collections.Generic;
using System.Threading.Tasks;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Status;

namespace Paramore.Brighter.Extensions.Tests.TestDoubles;

/// <summary>
/// A spy implementation of <see cref="IDispatcher"/> that records whether Receive was called
/// and tracks call ordering via a shared action log.
/// </summary>
public class SpyDispatcher : IDispatcher
{
    private readonly List<string> _actionLog;

    public bool ReceiveWasCalled { get; private set; }

    public SpyDispatcher(List<string> actionLog)
    {
        _actionLog = actionLog;
    }

    public IEnumerable<IAmAConsumer> Consumers => [];
    public HostName HostName { get; set; } = new("spy-dispatcher");

    public Task End() => Task.CompletedTask;
    public void Open(Subscription subscription) { }
    public void Open(SubscriptionName subscriptionName) { }

    public void Receive()
    {
        ReceiveWasCalled = true;
        _actionLog.Add("Receive");
    }

    public void Shut(Subscription subscription) { }
    public void Shut(SubscriptionName subscriptionName) { }
    public DispatcherStateItem[] GetState() => [];
    public void SetActivePerformers(string connectionName, int numberOfPerformers) { }
}
