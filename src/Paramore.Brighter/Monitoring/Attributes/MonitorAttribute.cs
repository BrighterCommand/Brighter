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
using Paramore.Brighter.Monitoring.Handlers;

namespace Paramore.Brighter.Monitoring.Attributes
{
    /// <summary>
    /// Class MonitorAttribute.
    /// Using this attribute indicates that you intend to monitor the handler. Monitoring implies that diagnostic information will be sent over the ControlBus to any subscribers.
    /// A configuration setting acts as a 'master switch' to turn monitoring on and off. The <see cref="MonitoringConfigurationSection"/> provides that switch.
    /// </summary>
    public class MonitorAttribute: RequestHandlerAttribute
    {
        private readonly string _handlerName;
        private readonly string _handlerFullAssemblyName;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandlerAttribute" /> class.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <param name="timing">The timing.</param>
        /// <param name="handlerType">The type of the monitored handler (used to extract the assembly qualified type name for instrumentation purposes)</param>
        public MonitorAttribute(int step, HandlerTiming timing, Type handlerType)
            : base(step, timing)
        {
            _handlerName = handlerType.FullName!;
            _handlerFullAssemblyName = handlerType.AssemblyQualifiedName!;
        }

        /// <summary>
        /// Initializers the parameters.
        /// </summary>
        /// <returns>System.Object[].</returns>
        public override object[] InitializerParams()
        {
            return new object[] {_handlerName, _handlerFullAssemblyName};
        }

        /// <summary>
        /// Gets the type of the handler.
        /// </summary>
        /// <returns>Type.</returns>
        public override Type GetHandlerType()
        {
            return typeof(MonitorHandler<>);
        }
    }
}
