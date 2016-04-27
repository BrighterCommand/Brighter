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
using Newtonsoft.Json.Converters;
using paramore.brighter.commandprocessor.monitoring.Handlers;

namespace paramore.brighter.commandprocessor.monitoring.Events
{
    /// <summary>
    /// What type of event are we recording
    /// </summary>
    public enum MonitorEventType
    {
        /// <summary>
        /// A policy implementing a circuit was tripped whilst running a handler
        /// </summary>
        BrokenCircuit,
        /// <summary>
        /// We have entered a handler
        /// </summary>
        EnterHandler,
        /// <summary>
        /// An exception was thrown by a handler
        /// </summary>
        ExceptionThrown,
        /// <summary>
        /// We have left a handler
        /// </summary>
        ExitHandler,
    }

    /// <summary>
    /// Class MonitorEvent.
    /// We monitor the execution of handlers. A <see cref="MonitorHandler{T}"/> will monitor filters that occur after it in the pipeline. It raises
    /// events before executing the remainder of the chain, after exiting the remainder of the chain, or because an exception was thrown in the chain.
    /// We capture the information on timing and the request.
    /// </summary>
    public class MonitorEvent : Event
    {
        /// <summary>
        /// Any exception that was thrown when processing the handler pipeline
        /// </summary>
        public Exception Exception { get; set; }
        
        /// <summary>
        /// Why was this event raised?
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public MonitorEventType EventType { get; private set; }

        /// <summary>
        /// When was this event raised?
        /// </summary>
        public DateTime EventTime { get; private set; }


        /// <summary>
        /// When was the duration in milliseconds?
        /// </summary>
        public int TimeElapsedMs { get; set; }

        /// <summary>
        //What was the handler that we raised this event for? 
        /// </summary>
        public string HandlerName { get; private set; }

        /// <summary>
        //What was the handler that we raised this event for, include full assembly path
        /// </summary>
        public string HandlerFullAssemblyName { get; set; }

        /// <summary>
        /// Which instance was this handler running on?
        /// </summary>
        public string InstanceName { get; set; }

        /// <summary>
        /// The serialied request - what were the parameters to this command?
        /// </summary>
        public string RequestBody { get; private set; }

        public MonitorEvent(
            string instanceName,
            MonitorEventType eventType, 
            string handlerName,
            string handlerFullName,
            string requestBody,
            DateTime eventTime,
            int elapsedMilliseconds,
            Exception exception = null
            )
            :base(Guid.NewGuid())
        {
            InstanceName = instanceName;
            EventType = eventType;
            HandlerName = handlerName;
            HandlerFullAssemblyName = handlerFullName;
            RequestBody = requestBody;
            EventTime = eventTime;
            TimeElapsedMs = elapsedMilliseconds;
            Exception = exception;
        }

    }
}
