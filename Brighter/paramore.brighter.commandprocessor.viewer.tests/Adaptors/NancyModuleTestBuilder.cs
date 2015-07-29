// ***********************************************************************
// Assembly         : paramore.brighter.commandprocessor
// Author           : ianp
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

using System.Collections.Generic;
using System.Linq;
using Nancy.Testing;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Handlers;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.Handlers;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors
{
    public static class NancyModuleTestBuilder
    {
        public static void MessagesModule(
            this ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator config,
            IMessageListViewModelRetriever messageListViewModelRetriever)
        {
            config.MessagesModule(messageListViewModelRetriever, new FakeHandlerFactory());
        }
        public static void MessagesModule(
            this ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator config,
            IMessageListViewModelRetriever messageListViewModelRetriever,
            IHandlerFactory handlerFactory)
        {
            config.Module<MessagesNancyModule>();
            config.Dependencies<IMessageListViewModelRetriever>(messageListViewModelRetriever);
            config.Dependencies<IHandlerFactory>(handlerFactory);
        }
        public static void StoresModule(
            this ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator config,
            IMessageStoreActivationStateListViewModelRetriever storeListRetriever,
            IMessageStoreViewerModelRetriever storeRetriever,
            IMessageListViewModelRetriever messageListRetriver)
        {
            config.Module<StoresNancyModule>();
            config.Dependencies<IMessageStoreActivationStateListViewModelRetriever>(storeListRetriever);
            config.Dependencies<IMessageStoreViewerModelRetriever>(storeRetriever);
            config.Dependencies<IMessageListViewModelRetriever>(messageListRetriver);
        }

        public static void StoresModule(this ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator config, 
                IEnumerable<MessageStoreActivationState> stores)
        {
            var listViewRetriever = new FakeActivationListModelRetriever(new MessageStoreActivationStateListModel(stores));
            var storeRetriever = new FakeMessageStoreViewerModelRetriever(new MessageStoreViewerModel(new FakeMessageStore(), stores.FirstOrDefault()));
            var messageRetriever = new FakeMessageListViewModelRetriever(new MessageListModel(new List<Message>()));

            config.StoresModule(listViewRetriever, storeRetriever, messageRetriever);
        }
    }
}