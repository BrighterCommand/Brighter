// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 07-01-2014
//
// Last Modified By : ian
// Last Modified On : 07-01-2014
// ***********************************************************************
// <copyright file="RequestLoggingHandler.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************
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
using Common.Logging;
using Newtonsoft.Json;

namespace paramore.brighter.commandprocessor
{
    /// <summary>
    /// Class RequestLoggingHandler.
    /// Logs a request to a <see cref="IHandleRequests"/> handler using the Common.Logging logger registered with the <see cref="CommandProcessor"/>
    /// The log shows the original <see cref="IRequest"/> properties as well as the timer handling.
    /// </summary>
    /// <typeparam name="TRequest">The type of the t request.</typeparam>
    public class RequestLoggingHandler<TRequest> : RequestHandler<TRequest> where TRequest : class, IRequest
    {
        private HandlerTiming timing;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestLoggingHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public RequestLoggingHandler(ILog logger)
            :base(logger)
        {}

        /// <summary>
        /// Initializes from attribute parameters.
        /// </summary>
        /// <param name="initializerList">The initializer list.</param>
        public override void InitializeFromAttributeParams(params object[] initializerList)
        {
            timing = (HandlerTiming)initializerList[0];
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override TRequest Handle(TRequest command)
        {
            LogCommand(command);
            return base.Handle(command);
        }
        
        private void LogCommand(TRequest request)
        {
            logger.Info(m => m("Logging handler pipeline call. Pipeline timing {0} target, for {1} with values of {2} at: {3}", timing.ToString(), typeof (TRequest), JsonConvert.SerializeObject(request),DateTime.UtcNow));
        }
    }
}
