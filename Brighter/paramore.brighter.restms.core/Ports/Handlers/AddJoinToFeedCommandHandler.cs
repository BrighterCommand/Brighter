// ***********************************************************************
// Assembly         : paramore.brighter.restms.core
// Author           : ian
// Created          : 10-21-2014
//
// Last Modified By : ian
// Last Modified On : 10-21-2014
// ***********************************************************************
// <copyright file="AddJoinToFeedCommandHandler.cs" company="">
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
using System.Linq;
using System.Transactions;
using Common.Logging;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;

namespace paramore.brighter.restms.core.Ports.Handlers
{
    public class AddJoinToFeedCommandHandler : RequestHandler<AddJoinToFeedCommand>
    {
        readonly IAmARepository<Feed> feedRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestHandler{TRequest}"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AddJoinToFeedCommandHandler(IAmARepository<Feed> feedRepository, ILog logger) : base(logger)
        {
            this.feedRepository = feedRepository;
        }

        /// <summary>
        /// Handles the specified command.
        /// </summary>
        /// <param name="addJoinToFeedCommand">The command.</param>
        /// <returns>TRequest.</returns>
        public override AddJoinToFeedCommand Handle(AddJoinToFeedCommand addJoinToFeedCommand)
        {
            using (var scope = new TransactionScope())
            {
                var feedUri = new Uri(addJoinToFeedCommand.FeedAddress);
                var feed = feedRepository.Find(f => f.Href == feedUri).FirstOrDefault();
                if (feed == null)
                {
                    throw new FeedDoesNotExistException();
                }

                //this creates the same join as added to the pipe - but is a different instance. It will compare equal by value
                var join = new Join(addJoinToFeedCommand.Pipe, new Uri(addJoinToFeedCommand.FeedAddress), new Address(addJoinToFeedCommand.AddressPattern));
                
                feed.AddJoin(join);

                scope.Complete();
            }
            return base.Handle(addJoinToFeedCommand);
        }
    }
}
