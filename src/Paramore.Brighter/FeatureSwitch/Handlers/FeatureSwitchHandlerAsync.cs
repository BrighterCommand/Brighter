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
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Actions;
using Paramore.Brighter.FeatureSwitch.Attributes;

namespace Paramore.Brighter.FeatureSwitch.Handlers
{
    /// <summary>
    /// Class FeatureSwitchHandler.
    /// The handler is injected into the pipeline if the <see cref="FeatureSwitchAttribute"/> is applied
    /// </summary>
    /// <typeparam name="TRequest">The type of the request</typeparam>
    public class FeatureSwitchHandlerAsync<TRequest> : RequestHandlerAsync<TRequest> where TRequest : class, IRequest
    {
        private Type? _handler;
        private FeatureSwitchStatus _status;
        private bool _dontAck;

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object?[] initializerList)
        {
            _handler = (Type?) initializerList[0];
            _status = (FeatureSwitchStatus?) initializerList[1] ?? FeatureSwitchStatus.Off;
            _dontAck = initializerList.Length > 2 && initializerList[2] is true;
        }

        /// <summary>
        /// Checks the status of the feature switch and either stops passes the command on to the next handler
        /// or stops execution of the feature switched handler.
        /// When dontAck is <c>true</c> and the feature is off, throws <see cref="DontAckAction"/> instead of
        /// silently returning, causing the message pump to leave the message unacknowledged on the channel.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>TRequest.</returns>
        /// <exception cref="DontAckAction">Thrown when the feature is off and dontAck is <c>true</c>.</exception>
        public override async Task<TRequest> HandleAsync(TRequest command, CancellationToken cancellationToken = default)
        {
            var featureEnabled = _status;

            if (featureEnabled is FeatureSwitchStatus.Config)
            {
                featureEnabled = Context?.FeatureSwitches?.StatusOf(_handler!) ?? FeatureSwitchStatus.On;
            }

            if (featureEnabled is FeatureSwitchStatus.Off)
            {
                if (_dontAck)
                    throw new DontAckAction($"Feature switch off for {_handler?.Name}; message not acknowledged");

                return command;
            }

            return await base.HandleAsync(command, cancellationToken);
        }
    }
}
