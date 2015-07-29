// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ian
// Created          : 25-03-2014
//
// Last Modified By : ian
// Last Modified On : 25-03-2014
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
using Nancy;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Handlers
{
    public class MessagesNancyModule : NancyModule
    {
        public MessagesNancyModule(IMessageListViewModelRetriever messageListViewModelRetriever)
            : base("/messages")
        {
            Get["/{storeName}/{pageNumber?1}"] = parameters =>
            {
                var logger = LogProvider.GetLogger("MessagesNancyModule");
                logger.Log(LogLevel.Debug, () => "GET on messages");

                string storeName = parameters.storeName;
                int pageNumber = parameters.pageNumber;
                var messageListModelResult = messageListViewModelRetriever.Get(storeName, pageNumber);

                if (!messageListModelResult.IsError)
                {
                    return Response.AsJson(messageListModelResult.Result);                    
                }
                switch (messageListModelResult.Error)
                {
                    case(MessageListModelError.StoreNotFound):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Unknown store {0}", storeName)), HttpStatusCode.NotFound);
                    
                    case (MessageListModelError.StoreMessageViewerNotImplemented):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Found store {0} does not implement IMessageStoreViewer", storeName)), HttpStatusCode.NotFound);

                    case (MessageListModelError.StoreMessageViewerGetException):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Unable to retrieve messages for store {0}", storeName)), HttpStatusCode.InternalServerError);

                    case (MessageListModelError.GetActivationStateFromConfigError):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Misconfigured Message Viewer, unable to retrieve messages for store {0}", storeName)), HttpStatusCode.InternalServerError);
                    default:
                        throw new SystemException("Code can't reach here");
                }
            };
        }
    }

}