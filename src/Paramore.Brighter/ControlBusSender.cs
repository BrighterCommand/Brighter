#region Licence
/* The MIT License (MIT)
Copyright © 2015 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Class ControlBusSender.
    /// This is really just a 'convenience' wrapper over a command processor to make it easy to configure two different command processors, one for normal messages the other for control messages.
    /// Why? The issue arises because an application providing a lot of monitoring messages might find that the load of those control messages begins to negatively impact the throughput of normal messages.
    /// To avoid this you can put control messages over a seperate broker. (There are some availability advantages here too).
    /// But many IoC containers make your life hard when you do this, as you have to indicate that you want to build the MonitorHandler with one command processor and the other handlers with another
    /// Wrapping the Command Processor in this class helps to alleviate that issue, by taking a dependency on a seperate interface.
    /// What goes over a control bus?
    /// The Control Bus is used carry the following types of messages:
    //      Configuration - Allows runtime configuration of a service activator node, including stopping and starting, adding and removing of channels, control of resources allocated to channels.
    //      Heartbeat - A ping to service activator node to determine if it is still 'alive'. The node returns a message over a private queue established by the caller.The message also displays diagnostic information on the health of the node.
    //      Exceptions— Any exceptions generated on the node may be sent by the Control Bus to monitoring systems.
    //      Statistics— Each service activator node broadcasts statistics about the processig of messages which can be collated by a listener to the control bus to calculate the numnber of messages proceses, average throughput, average time to process a message, and so on.This data is split out by message type, so we can aggregate results.
    /// 
    /// </summary>
    public class ControlBusSender : IAmAControlBusSender, IAmAControlBusSenderAsync, IDisposable
    {
        /// <summary>
        /// The command processor that underlies the control bus; we only use the Post method
        /// </summary>
        private readonly CommandProcessor _commandProcessor;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ControlBusSender"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        public ControlBusSender(CommandProcessor commandProcessor)
        {
            _commandProcessor = commandProcessor;
        }

        /// <summary>
        /// Posts the specified request.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request">The request.</param>
        public void Post<T>(T request) where T : class, IRequest
        {
            _commandProcessor.Post(request);
        }

        public async Task PostAsync<T>(T request, bool continueOnCapturedContext = false, CancellationToken cancellationToken = default(CancellationToken)) where T : class, IRequest
        {
            await _commandProcessor.PostAsync(request, continueOnCapturedContext, cancellationToken).ConfigureAwait(continueOnCapturedContext);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _commandProcessor.Dispose();

            _disposed = true;
        }
    }
}