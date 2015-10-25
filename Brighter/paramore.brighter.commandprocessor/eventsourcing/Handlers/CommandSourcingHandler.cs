﻿// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 06-02-2015
//
// Last Modified By : ian
// Last Modified On : 06-03-2015
// ***********************************************************************
// <copyright file="CommandSourcingHandler.cs" company="">
//     Copyright (c) . All rights reserved.
// </copyright>
// <summary></summary>
// ***********************************************************************

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
using System.Threading.Tasks;
using Newtonsoft.Json;
using paramore.brighter.commandprocessor.Logging;

namespace paramore.brighter.commandprocessor.eventsourcing.Handlers
{
    /// <summary>
    /// Class CommandSourcingHandler.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class CommandSourcingHandler<T> : RequestHandler<T> where T: class, IRequest
    {
        private readonly IAmACommandStore _commandStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}" /> class.
        /// </summary>
        /// <param name="commandStore">The store for commands that pass into the system</param>
        public CommandSourcingHandler(IAmACommandStore commandStore)
            : this(commandStore, LogProvider.GetCurrentClassLogger())
        {}


        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}" /> class.
        /// </summary>
        /// <param name="commandStore">The store for commands that pass into the system</param>
        /// <param name="logger">The logger.</param>
        public CommandSourcingHandler(IAmACommandStore commandStore, ILog logger) : base(logger)
        {
            _commandStore = commandStore;
        }

        /// <summary>
        /// Logs the command we received to the command store. Today we are forced  
        /// </summary>
        /// <param name="command">The command that we want to store.</param>
        /// <returns>The parameter to allow request handlers to be chained together in a pipeline</returns>
        public override T Handle(T command) 
        {
            logger.DebugFormat("Writing command {0} to the Command Store", command.Id);

            _commandStore.Add(command);

            return base.Handle(command);
        }

    }
}
