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
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.monitoring.Configuration;
using paramore.brighter.commandprocessor.monitoring.Events;

namespace paramore.brighter.commandprocessor.monitoring.Handlers
{
    /// <summary>
    /// Class MonitorHandler.
    /// The MonitorHandler raises an event via the Control Bus when we enter, exit, and if any exceptions are thrown, provided that monitoring has been enabled in the configuration.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class MonitorHandler<T> : RequestHandler<T> where T: class, IRequest
    {
        readonly IAmAControlBusSender _controlBusSender;
        private readonly bool _isMonitoringEnabled;
        private string _handlerName;
        private readonly string _instanceName;
        private string _handlerFullAssemblyName;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="controlBusSender">The control bus command processor, to post over</param>
        /// <param name="monitorConfiguration"></param>
        public MonitorHandler(IAmAControlBusSender controlBusSender, MonitorConfiguration monitorConfiguration)
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
            _handlerName = (string) initializerList[0];
            _handlerFullAssemblyName = (string)initializerList[1];
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override T Handle(T command)
        {
            if (_isMonitoringEnabled)
            {
                var timeBeforeHandle = Clock.Now();
                try
                {
                    _controlBusSender.Post(
                        new MonitorEvent(
                            _instanceName, 
                            MonitorEventType.EnterHandler, 
                            _handlerName, 
                            _handlerFullAssemblyName,
                            JsonConvert.SerializeObject(command), 
                            timeBeforeHandle,
                            0));

                    base.Handle(command);

                    var timeAfterHandle = Clock.Now();
                    _controlBusSender.Post(
                        new MonitorEvent(
                            _instanceName, 
                            MonitorEventType.ExitHandler, 
                            _handlerName,
                            _handlerFullAssemblyName,
                            JsonConvert.SerializeObject(command), 
                            timeAfterHandle,
                            (timeAfterHandle-timeBeforeHandle).Milliseconds));
                        
                    return command;
                }
                catch (Exception e)
                {
                    var timeOnException = Clock.Now();
                    _controlBusSender.Post(
                        new MonitorEvent(
                            _instanceName, 
                            MonitorEventType.ExceptionThrown, 
                            _handlerName,
                            _handlerFullAssemblyName,
                            JsonConvert.SerializeObject(command), 
                            timeOnException,
                            (timeOnException - timeBeforeHandle).Milliseconds,
                            e));
                    throw;
                }

            }

            return base.Handle(command);
        }

    }
}
