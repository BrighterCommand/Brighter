// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-01-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="NewFeedHandler.cs" company="">
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
    public class AddFeedCommandHandler : RequestHandler<AddFeedCommand>
    {
        readonly IAmARepository<Feed> feedRepository;
        readonly IAmACommandProcessor commandProcessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="feedRepository"></param>
        /// <param name="commandProcessor"></param>
        public AddFeedCommandHandler(ILog logger, IAmARepository<Feed> feedRepository, IAmACommandProcessor commandProcessor) : base(logger)
        {
            this.feedRepository = feedRepository;
            this.commandProcessor = commandProcessor;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddFeedCommand Handle(AddFeedCommand command)
        {
            using (var scope = new TransactionScope())
            {
                var existingFeed = feedRepository[new Identity(command.Name)];
                if (existingFeed != null)
                {
                    throw new FeedAlreadyExistsException("The feed has already been created");
                }
                
                var feed = new Feed(
                    name: new Name(command.Name),
                    feedType: (FeedType) Enum.Parse(typeof (FeedType), command.Type),
                    title: new Title(command.Title),
                    license: new Name(command.License)
                    );

                feedRepository.Add(feed);
                scope.Complete();
            }
            commandProcessor.Send(new AddFeedToDomainCommand(command.DomainName, command.Name));

            return base.Handle(command);
        }
    }
}
