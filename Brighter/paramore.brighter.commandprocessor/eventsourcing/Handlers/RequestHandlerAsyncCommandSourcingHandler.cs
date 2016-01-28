// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 2016-01-08
//
// Last Modified By : ian
// Last Modified On : 2016-01-08
// ***********************************************************************
// <copyright file="CommandSourcingHandler.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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

using System.Threading;
using System.Threading.Tasks;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.eventsourcing.Handlers
{
    /// <summary>
    /// Class AsyncCommandSourcingHandler.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RequestHandlerAsyncCommandSourcingHandler<T> : RequestHandlerAsync<T> where T : class, IRequest
    {
        private readonly IAmAnAsyncCommandStore _commandStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandlerAsyncCommandSourcingHandler{T}" /> class.
        /// </summary>
        /// <param name="commandStore">The store for commands that pass into the system</param>
        public RequestHandlerAsyncCommandSourcingHandler(IAmAnAsyncCommandStore commandStore)
            : this(commandStore, LogProvider.GetCurrentClassLogger())
        { }


        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandlerAsyncCommandSourcingHandler{T}" /> class.
        /// </summary>
        /// <param name="commandStore">The store for commands that pass into the system</param>
        /// <param name="logger">The logger.</param>
        public RequestHandlerAsyncCommandSourcingHandler(IAmAnAsyncCommandStore commandStore, ILog logger) : base(logger)
        {
            _commandStore = commandStore;
        }

        /// <summary>
        /// Awaitably logs the command we received to the command store.
        /// </summary>
        /// <param name="command">The command that we want to store.</param>
        /// <param name="ct">Allows the caller to cancel the pipeline if desired</param>
        /// <returns>The parameter to allow request handlers to be chained together in a pipeline</returns>
        public override async Task<T> HandleAsync(T command, CancellationToken? ct = null)
        {
            logger.DebugFormat("Writing command {0} to the Command Store", command.Id);

            //TODO: We should not use an infinite timeout here - how to configure
            await _commandStore.AddAsync(command, -1, ct).ConfigureAwait(ContinueOnCapturedContext);

            return await base.HandleAsync(command, ct).ConfigureAwait(ContinueOnCapturedContext);
        }
    }
}
