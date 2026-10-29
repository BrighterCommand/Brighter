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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MessagingGateway.AzureServiceBus.AzureServiceBusWrappers;

/// <summary>
/// A receiver that can persist application properties when releasing a message for redelivery.
/// </summary>
/// <remarks>
/// Topic consumers require this capability for immediate retries so the updated handled count
/// survives redelivery. Custom receiver providers can implement it alongside the existing wrapper contract.
/// </remarks>
public interface IAmAServiceBusRetryReceiver : IServiceBusReceiverWrapper
{
    /// <summary>
    /// Releases the message lock and updates its application properties atomically at the broker.
    /// </summary>
    /// <param name="lockToken">The lock token of the received message.</param>
    /// <param name="propertiesToModify">Application properties to persist before redelivery.</param>
    /// <param name="cancellationToken">Cancels the abandon operation.</param>
    /// <returns>A task that completes when the broker has accepted the operation.</returns>
    Task AbandonAsync(string lockToken, IDictionary<string, object> propertiesToModify,
        CancellationToken cancellationToken = default);
}
