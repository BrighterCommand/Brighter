// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-21-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="AddPipeCommandHandler.cs" company="">
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

using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    /// <summary>
    /// </summary>
    public class AddPipeCommandHandler : RequestHandler<AddPipeCommand>
    {
        readonly IAmARepository<Pipe> pipeRepository;
        readonly IAmACommandProcessor commandProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="AddPipeCommandHandler"/> class.
        /// </summary>
        /// <param name="pipeRepository">The pipe repository.</param>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="logger">The logger.</param>
        public AddPipeCommandHandler(IAmARepository<Pipe> pipeRepository, IAmACommandProcessor commandProcessor, ILog logger) : base(logger)
        {
            this.pipeRepository = pipeRepository;
            this.commandProcessor = commandProcessor;
        }

        #region Overrides of RequestHandler<AddPipeCommand>

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddPipeCommand Handle(AddPipeCommand command)
        {
            var pipe = new Pipe(command.Id.ToString(), command.Type, command.Title);
            using (var scope = new TransactionScope())
            {
                pipeRepository.Add(pipe);
                scope.Complete();
            }

            if (pipe.Type == PipeType.Default)
            {
                //a default pipe always hasa join to the default feed.
                //this allows a sender to address us directly, by name
                commandProcessor.Send(new AddJoinToPipeCommand(pipe.Name.Value, GetDefaultFeedUri(), pipe.Name.Value));
            }

            commandProcessor.Send(new AddPipeToDomainCommand(command.DomainName, pipe.Name.Value));

            return base.Handle(command);
        }

        string GetDefaultFeedUri()
        {
            return new Feed(
                feedType: FeedType.Default,
                name: new Name("default"),
                title: new Title("Default feed")
                ).Href.AbsoluteUri;
        }

        #endregion
    }
}
