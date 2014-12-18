// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 11-05-2014
//
// Last Modified By : ian
// Last Modified On : 11-05-2014
// ***********************************************************************
// <copyright file="AddJoinToPipeCommandHandler.cs" company="">
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
using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    /// <summary>
    /// Class AddJoinToPipeCommandHandler.
    /// </summary>
    public class AddJoinToPipeCommandHandler : RequestHandler<AddJoinToPipeCommand>
    {
        readonly IAmARepository<Pipe> pipeRepository;
        readonly IAmACommandProcessor commandProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}" /> class.
        /// </summary>
        /// <param name="pipeRepository">The pipe repository.</param>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="logger">The logger.</param>
        public AddJoinToPipeCommandHandler(IAmARepository<Pipe> pipeRepository, IAmACommandProcessor commandProcessor, ILog logger) : base(logger)
        {
            this.pipeRepository = pipeRepository;
            this.commandProcessor = commandProcessor;
        }

        /// <summary>
        /// Handles the command to add a join.
        /// We follow a sequence: add to the pipe, add to the feed, add to the join repository. Each step is a transactional boundary, and we use events
        /// to pass control between the steps. The goal here is that failure is safe. If the add to pipe fails, the feed will not try to add to a pipe
        /// that is not aware of the join. If the add to feed fails, the pipe will just get no messages, and this should be discoverable by finding that the
        /// join does not exist. If the add to pipe and add to feed succeed, but the add to join repo fails you will get messages, but not be able to query the join
        /// directly.
        /// </summary>
        /// <param name="addJoinToPipeCommand">The command.</param>
        /// <returns>TRequest.</returns>
        /// <exception cref="PipeDoesNotExistException"></exception>
        public override AddJoinToPipeCommand Handle(AddJoinToPipeCommand addJoinToPipeCommand)
        {
            Pipe pipe;
            using (var scope = new TransactionScope())
            {
                pipe = pipeRepository[new Identity(addJoinToPipeCommand.PipeName)];

                if (pipe == null)
                {
                    throw new PipeDoesNotExistException(string.Format("Pipe {0} not found", addJoinToPipeCommand.PipeName));
                }

                //this creates the same join as added to the feed - but is a different instance. It will compare equal by value
                var join = new Join(pipe, new Uri(addJoinToPipeCommand.FeedAddress), new Address(addJoinToPipeCommand.AddressPattern));

                pipe.AddJoin(join);

                scope.Complete();
            }

            commandProcessor.Send(new AddJoinToFeedCommand(pipe, addJoinToPipeCommand.FeedAddress, addJoinToPipeCommand.AddressPattern));
            return base.Handle(addJoinToPipeCommand);
        }
    }
}
