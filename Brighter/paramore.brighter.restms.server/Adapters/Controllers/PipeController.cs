// ***********************************************************************
// Assembly         : paramore.brighter.restms.server
// Author           : ian
// Created          : 11-06-2014
//
// Last Modified By : ian
// Last Modified On : 11-06-2014
// ***********************************************************************
// <copyright file="PipeController.cs" company="">
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
using System.Runtime.Remoting.Messaging;
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
    /// Class PipeController.
    /// </summary>
    public class PipeController : ApiController
    {
        readonly IAmACommandProcessor commandProcessor;
        readonly IAmARepository<Pipe> pipeRepository;

        /// <summary>
        /// Initializes a new instance of the <see cref="PipeController"/> class.
        /// </summary>
        /// <param name="commandProcessor">The command processor.</param>
        /// <param name="pipeRepository">The pipe repository.</param>
        public PipeController(IAmACommandProcessor commandProcessor, IAmARepository<Pipe> pipeRepository)
        {
            this.commandProcessor = commandProcessor;
            this.pipeRepository = pipeRepository;
        }


        /// <summary>
        /// Gets the specified pipe name.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        /// <returns>RestMSPipe.</returns>
        [Route("restms/pipe/{pipeName}")]
        [HttpGet]
        [PipeDoesNotExistExceptionFilter]
        public RestMSPipe Get(string pipeName)
        {
            var retriever = new PipeRetriever(pipeRepository);
            return retriever.Retrieve(new Name(pipeName));
        }


        [Route("restms/pipe/{pipeName}")]
        [PipeDoesNotExistExceptionFilter]
        [FeedDoesNotExistExceptionFilter]
        public HttpResponseMessage Post(string pipeName, RestMSJoin join)
        {
            var addJoinCommand = new AddJoinToPipeCommand(pipeName, join.Feed, join.Address);
            commandProcessor.Send(addJoinCommand);

            var retriever = new PipeRetriever(pipeRepository);
            var item = retriever.Retrieve(new Name(pipeName));
            var response = Request.CreateResponse(HttpStatusCode.Created, item);
            response.Headers.Location = new Uri(item.Href);
            return response;
        }


        /// <summary>
        /// Deletes the specified pipe name.
        /// </summary>
        /// <param name="pipeName">Name of the pipe.</param>
        [Route("restms/pipe/{pipeName}")]
        [HttpDelete]
        [PipeDoesNotExistExceptionFilter]
        public HttpResponseMessage Delete(string pipeName)
        {
            var deleteCommand = new DeletePipeCommand(pipeName);
            commandProcessor.Send(deleteCommand);

            return Request.CreateResponse(HttpStatusCode.OK);
        }



    }
}
