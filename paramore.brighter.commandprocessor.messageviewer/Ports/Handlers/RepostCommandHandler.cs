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
using System.Collections.Generic;
using Paramore.Brighter.MessageViewer.Ports.Domain;

namespace Paramore.Brighter.MessageViewer.Ports.Handlers
{
    public class RepostCommand : ICommand
    {
        public List<string> MessageIds { get; set; }
        public string StoreName { get; set; }
    }

    public class RepostCommandHandler : IHandleCommand<RepostCommand>
    {
        private readonly IMessageStoreViewerFactory _messageStoreViewerFactory;
        private readonly IMessageProducerFactoryProvider _messageProducerFactoryProvider;
        private readonly IAmAMessageRecoverer _messageRecoverer;

        public RepostCommandHandler(IMessageStoreViewerFactory messageStoreViewerFactory,
                                    IMessageProducerFactoryProvider messageProducerFactoryProvider, 
                                    IAmAMessageRecoverer messageRecoverer)
        {
            _messageStoreViewerFactory = messageStoreViewerFactory;
            _messageProducerFactoryProvider = messageProducerFactoryProvider;
            _messageRecoverer = messageRecoverer;
        }

        /// <exception cref="SystemException">Store not found / Mis-configured viewer broker</exception>
        public void Handle(RepostCommand command)
        {
            CheckMessageIds(command);            
            var messageStore = GetMessageStoreFromConfig(command);
            var foundProducer = GetMessageProducerFromConfig(_messageProducerFactoryProvider);

            _messageRecoverer.Repost(command.MessageIds, messageStore, foundProducer);
        }

        private IAmAMessageProducer GetMessageProducerFromConfig(IMessageProducerFactoryProvider messageProducerFactoryProvider)
        {
            var messageProducerFactory = messageProducerFactoryProvider.Get();
            if (messageProducerFactory == null)
            {
                throw new Exception("Mis-configured viewer - no message producer found");
            }
            IAmAMessageProducer messageProducer = null;
            Exception foundException = null;
            try
            {
                messageProducer = messageProducerFactory.Create();
            }
            catch (Exception e)
            {
                foundException = e;
            }
            if (messageProducer == null)
            {
                string message = "Mis-configured viewer - cannot create found message producer";
                if (foundException != null)
                {
                    message += ". " + foundException.Message;
                }
                throw new Exception(message);
            }
            return messageProducer;
        }

        private IAmAMessageStore<Message> GetMessageStoreFromConfig(RepostCommand command)
        {
            IAmAMessageStore<Message> messageStore = _messageStoreViewerFactory.Connect(command.StoreName);
            if (messageStore == null)
            {
                throw new Exception("Error " + RepostCommandHandlerError.StoreNotFound);
            }
            return messageStore;
        }

        private static void CheckMessageIds(RepostCommand command)
        {
            if (command.MessageIds == null)
            {
                throw new Exception("Error null MessageIds");
            }
        }
    }

    internal enum RepostCommandHandlerError
    {
        StoreNotFound
    }
}