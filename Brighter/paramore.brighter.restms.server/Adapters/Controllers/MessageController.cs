// ***********************************************************************
// Assembly         : paramore.brighter.restms.server
// Author           : ian
// Created          : 11-06-2014
//
// Last Modified By : ian
// Last Modified On : 11-06-2014
// ***********************************************************************
// <copyright file="MessageController.cs" company="">
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
using System.Net;
using System.Net.Http;
using System.Web.Http;
using paramore.brighter.commandprocessor;
using paramore.brighter.restms.core.Model;
using paramore.brighter.restms.core.Ports.Commands;
using paramore.brighter.restms.core.Ports.Common;
using paramore.brighter.restms.core.Ports.Resources;
using paramore.brighter.restms.core.Ports.ViewModelRetrievers;
using paramore.brighter.restms.server.Adapters.Filters;

namespace paramore.brighter.restms.server.Adapters.Controllers
{
    /// <summary>
    /// Class MessageController.
    /// </summary>
    [Authorize]
    public class MessageController : ApiController
    {
        readonly IAmACommandProcessor commandProcessor;
        readonly IAmARepository<Pipe> pipeRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageController"/> class.
        /// </summary>
        /// <param name="commandProcessor"></param>
        /// <param name="pipeRepository">The pipe repository.</param>
        public MessageController(IAmACommandProcessor commandProcessor, IAmARepository<Pipe> pipeRepository)
        {
            this.commandProcessor = commandProcessor;
            this.pipeRepository = pipeRepository;
        }

        /// <summary>
        /// Gets the specified pipe name.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        /// <param name="messageName">Name of the message.</param>
        /// <returns>RestMSMessage.</returns>
        [HttpGet]
        [PipeDoesNotExistExceptionFilter]
        public RestMSMessage Get(string pipeName, string messageName)
        {
            var retriever = new MessageRetriever(pipeRepository);
            return retriever.Retrieve(new Name(pipeName), Guid.Parse(messageName));
        }

        [HttpDelete]
        [PipeDoesNotExistExceptionFilter]
        public void Delete(string pipeName, string messageName)
        {
            var deleteMessageCommand = new DeleteMessageCommand(
                pipeName,
                Guid.Parse(messageName));

            commandProcessor.Send(deleteMessageCommand);

            Request.CreateResponse(HttpStatusCode.OK);
        }

    }
}
