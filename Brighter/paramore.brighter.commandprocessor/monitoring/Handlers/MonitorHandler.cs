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
using paramore.brighter.commandprocessor.monitoring.Events;

namespace paramore.brighter.commandprocessor.monitoring.Handlers
{
    public class MonitorHandler<T> : RequestHandler<T> where T: class, IRequest
    {
        readonly IAmAControlBusSender _controlBusSender;
        private bool _isMonitoringEnabled;
        private string _handlerName;
        private string _instanceName;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger</param>
        /// <param name="controlBusSender">The control bus command processor, to post over</param>
        public MonitorHandler(ILog logger, IAmAControlBusSender controlBusSender) : base(logger)
        {
            _controlBusSender = controlBusSender;
        }

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            _isMonitoringEnabled = (bool) initializerList[0];
            _handlerName = (string) initializerList[1];
            _instanceName = (string) initializerList[2];
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
                try
                {
                    _controlBusSender.Post(
                        new MonitorEvent(
                            _instanceName, 
                            MonitorEventType.EnterHandler, 
                            _handlerName, 
                            JsonConvert.SerializeObject(command), 
                            Clock.Now().GetValueOrDefault()));

                    base.Handle(command);

                    _controlBusSender.Post(
                        new MonitorEvent(
                            _instanceName, 
                            MonitorEventType.ExitHandler, 
                            _handlerName, 
                            JsonConvert.SerializeObject(command), 
                            Clock.Now().GetValueOrDefault()));
                        
                    return command;
                }
                catch (Exception e)
                {
                    _controlBusSender.Post(
                        new MonitorEvent(
                            _instanceName, 
                            MonitorEventType.ExceptionThrown, 
                            _handlerName, 
                            JsonConvert.SerializeObject(command), 
                            Clock.Now().GetValueOrDefault(),
                            e));
                    throw;
                }

            }

            return base.Handle(command);
        }

    }
}
