#region Licence
/* The MIT License (MIT)
Copyright © 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.FeatureSwitch.Handlers;

namespace Paramore.Brighter.FeatureSwitch.Attributes
{
    /// <summary>
    /// Class FeatureSwitchAttribute.
    /// This attribute supports adding feature switches on a handler. It is intended to allow control over which handlers can be used at
    /// run-time using <see cref="FeatureSwitchStatus.Config"/> or compile time using <see cref="FeatureSwitchStatus.On"/> and <see cref="FeatureSwitchStatus.Off"/>.
    /// </summary>
    public class FeatureSwitchAttribute : RequestHandlerAttribute
    {
        private readonly Type _handler;
        private readonly FeatureSwitchStatus _status;
        private readonly bool _dontAck;

        /// <summary>
        /// Initialises a new instance of the <see cref="FeatureSwitchAttribute"/> class.
        /// </summary>
        /// <param name="handler">The handler to feature switch</param>
        /// <param name="status">The status of the feature switch</param>
        /// <param name="step">The step.</param>
        /// <param name="timing">The timing.</param>
        /// <param name="dontAck">When <c>true</c> and the feature is switched off, throw <see cref="Actions.DontAckAction"/>
        /// instead of silently consuming the message. Defaults to <c>false</c>.</param>
        public FeatureSwitchAttribute(Type handler, FeatureSwitchStatus status, int step, HandlerTiming timing = HandlerTiming.Before, bool dontAck = false) : base(step, timing)
        {
            _handler = handler;
            _status = status;
            _dontAck = dontAck;
        }

        /// <summary>
        /// Initialises the params
        /// </summary>
        /// <returns>System.Object[]</returns>
        public override object[] InitializerParams()
        {
            return new object[] { _handler, _status, _dontAck };
        }

        /// <summary>
        /// Gets the type of the handler
        /// </summary>
        /// <returns></returns>
        public override Type GetHandlerType()
        {
            return typeof(FeatureSwitchHandler<>);
        }
    }
}
