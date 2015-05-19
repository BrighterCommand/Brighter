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
    public class StoresNancyModule : NancyModule
    {
        private readonly ILog _logger;

        public StoresNancyModule(
            IMessageStoreActivationStateListViewModelRetriever messageStoreActivationStateListViewModelRetriever,
            IMessageStoreViewerModelRetriever messageStoreViewerModelRetriever,
            IMessageListViewModelRetriever messageSearchListItemRetriever)
            : base("/stores")
        {
            _logger = LogProvider.GetLogger("StoresNancyModule");

            Get["/"] = x =>
            {
                _logger.Log(LogLevel.Debug, () => "GET on stores");

                var result = messageStoreActivationStateListViewModelRetriever.Get();
                if (!result.IsError)
                {
                    return Response.AsJson(result.Result);
                }
                switch (result.Error)
                {
                    case (MessageStoreActivationStateListModelError.GetActivationStateFromConfigError):
                        return Response.AsJson(new MessageViewerError("Misconfigured message viewer - unable to read config store"), HttpStatusCode.InternalServerError);
                    default:
                        throw new SystemException("Code can't reach here");
                }
            };
            
            Get["/{name}"] = parameters =>
            {
                string storeName = parameters.Name;
                _logger.Log(LogLevel.Debug, () => "GET store " + storeName);

                var result = messageStoreViewerModelRetriever.Get(storeName);
                if (!result.IsError)
                {
                    return Response.AsJson(result.Result);
                }
                switch (result.Error)
                {
                    case (MessageStoreViewerModelError.StoreNotFound):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Unknown store {0}", storeName)), HttpStatusCode.NotFound);

                    case (MessageStoreViewerModelError.StoreMessageViewerNotImplemented):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Found store {0} does not implement IMessageStoreViewer", storeName)), HttpStatusCode.NotFound);

                    case (MessageStoreViewerModelError.StoreMessageViewerGetException):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Unable to retrieve detail for store {0}", storeName)), HttpStatusCode.InternalServerError);

                    case (MessageStoreViewerModelError.GetActivationStateFromConfigError):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Misconfigured Message Viewer, unable to retrieve detail for store {0}", storeName)), HttpStatusCode.InternalServerError);
                    default:
                        throw new SystemException("Code can't reach here");
                }
            };

            Get["/search/{storeName}/{searchTerm}"] = parameters =>
            {
                string messageStoreName = parameters.storeName;
                string searchTerm = parameters.searchTerm;
                _logger.Log(LogLevel.Debug, () => string.Format("SEARCH store {0} with '{1}'", messageStoreName, searchTerm));

                var searchModelResult = messageSearchListItemRetriever.Filter(messageStoreName, searchTerm);
                if (!searchModelResult.IsError)
                {
                    return Response.AsJson(searchModelResult.Result);
                }
                switch (searchModelResult.Error)
                {
                    case (MessageListModelError.StoreNotFound):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Unknown store {0}", messageStoreName)), HttpStatusCode.NotFound);

                    case (MessageListModelError.StoreMessageViewerNotImplemented):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Found store {0} does not implement IMessageStoreViewer", messageStoreName)), HttpStatusCode.NotFound);

                    case (MessageListModelError.StoreMessageViewerGetException):
                        return Response.AsJson(new MessageViewerError(
                            string.Format("Unable to retrieve messages for store {0}", messageStoreName)), HttpStatusCode.InternalServerError);
                    default:
                        throw new SystemException("Code can't reach here");
                }
            };
        }
    }
}