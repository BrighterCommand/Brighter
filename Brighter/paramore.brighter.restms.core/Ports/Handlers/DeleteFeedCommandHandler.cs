// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-09-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
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
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    /// <summary>
    /// Class DeleteFeedCommandHandler.
    /// </summary>
    public class DeleteFeedCommandHandler : RequestHandler<DeleteFeedCommand>
    {
        private readonly IAmARepository<Feed> _feedRepository;
        private readonly IAmACommandProcessor _commandProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public DeleteFeedCommandHandler(ILog logger) : base(logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteFeedCommandHandler"/> class.
        /// </summary>
        /// <param name="feedRepository">The feed repository.</param>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="log">The log.</param>
        public DeleteFeedCommandHandler(IAmARepository<Feed> feedRepository, IAmACommandProcessor commandProcessor, ILog log) : base(log)
        {
            _feedRepository = feedRepository;
            _commandProcessor = commandProcessor;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override DeleteFeedCommand Handle(DeleteFeedCommand command)
        {
            using (var scope = new TransactionScope())
            {
                var feed = _feedRepository[new Identity(command.FeedName)];
                if (feed == null)
                {
                    throw new FeedDoesNotExistException();
                }
                else if (feed.Type == FeedType.Default)
                {
                    throw new InvalidOperationException("You cannot delete the default feed");
                }

                _feedRepository.Remove(new Identity(command.FeedName));
                scope.Complete();
            }

            _commandProcessor.Send(new RemoveFeedFromDomainCommand(command.FeedName));

            return base.Handle(command);
        }
    }
}
