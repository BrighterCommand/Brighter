#region Licence
/* The MIT License (MIT)
Copyright © 2016 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Paramore.Brighter.Monitoring.Configuration;
using Paramore.Brighter.Monitoring.Events;
using Paramore.Brighter.Time;

namespace Paramore.Brighter.Monitoring.Handlers
{
    public class MonitorHandlerAsync<T> : RequestHandlerAsync<T> where T : class, IRequest
    {
        private readonly IAmAControlBusSenderAsync _controlBusSender;
        private bool _isMonitoringEnabled;
        private string _handlerName;
        private string _instanceName;
        private string _handlerFullAssemblyName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorHandlerAsync{T}"/> class.
        /// </summary>
        /// <param name="controlBusSender">The control bus command processor, to post over</param>
        /// <param name="monitorConfiguration"></param>
        public MonitorHandlerAsync(IAmAControlBusSenderAsync controlBusSender, MonitorConfiguration monitorConfiguration)
        {
            _controlBusSender = controlBusSender;
            _isMonitoringEnabled = monitorConfiguration.IsMonitoringEnabled;
            _instanceName = monitorConfiguration.InstanceName;
        }

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            _handlerName = (string)initializerList[0];
            _handlerFullAssemblyName = (string)initializerList[1];
        }

        /// <summary>
        /// Awaitably handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken">Allow the caller to cancel the operation (optional)</param>
        /// <returns><see cref="Task"/>.</returns>
        public override async Task<T> HandleAsync(T command, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (!_isMonitoringEnabled) return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

            ExceptionDispatchInfo capturedException = null;
            var timeBeforeHandle = Clock.Now();
            try
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await _controlBusSender.PostAsync(
                        new MonitorEvent(
                            _instanceName,
                            MonitorEventType.EnterHandler,
                            _handlerName,
                            _handlerFullAssemblyName,
                            JsonConvert.SerializeObject(command),
                            timeBeforeHandle,
                            0),
                        ContinueOnCapturedContext, cancellationToken)
                        .ConfigureAwait(ContinueOnCapturedContext);
                }

                await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);

                if (!cancellationToken.IsCancellationRequested)
                {
                    var timeAfterHandle = Clock.Now();
                    await _controlBusSender.PostAsync(
                        new MonitorEvent(
                            _instanceName,
                            MonitorEventType.ExitHandler,
                            _handlerName,
                            _handlerFullAssemblyName,
                            JsonConvert.SerializeObject(command),
                            timeAfterHandle,
                            (timeAfterHandle - timeBeforeHandle).Milliseconds),
                        ContinueOnCapturedContext, cancellationToken)
                        .ConfigureAwait(ContinueOnCapturedContext);
                }

                return command;
            }
            catch (Exception e)
            {
                capturedException = ExceptionDispatchInfo.Capture(e);
            }

            //C#5 does not support await in a catch block; once we move to C#6 we can re-inline and drop use of ExceptionDispatchInfo
            if (capturedException != null)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    var timeOnException = Clock.Now();
                    //can't await inside a catch block
                    await _controlBusSender.PostAsync(
                        new MonitorEvent(
                            _instanceName,
                            MonitorEventType.ExceptionThrown,
                            _handlerName,
                            _handlerFullAssemblyName,
                            JsonConvert.SerializeObject(command),
                            timeOnException,
                            (timeOnException - timeBeforeHandle).Milliseconds,
                            capturedException.SourceException),
                        ContinueOnCapturedContext, cancellationToken)
                        .ConfigureAwait(ContinueOnCapturedContext);
                }

                capturedException.Throw();
            }

            return await base.HandleAsync(command, cancellationToken).ConfigureAwait(ContinueOnCapturedContext);
        }
    }
}
