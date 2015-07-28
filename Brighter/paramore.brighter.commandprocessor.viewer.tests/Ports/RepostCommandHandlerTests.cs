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
using System.Linq;
using Machine.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messageviewer.Ports.Handlers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports
{
    [Subject(typeof (RepostCommandHandler))]
    public class RepostCommandHandlerTests
    {
        public class When_reposting_messages
        {
            private Establish _context = () =>
            {
                var fakeStore = new FakeMessageStoreWithViewer();
                _messageToRepost = new Message(new MessageHeader(Guid.NewGuid(), "a topic", MessageType.MT_COMMAND, DateTime.UtcNow), new MessageBody("body"));
                fakeStore.Add(_messageToRepost);
                var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);

                _command = new RepostCommand{MessageIds = new List<string>{_messageToRepost.Header.Id.ToString()}, StoreName = _storeName};
                _fakeMessageProducer = new FakeMessageProducer();
                _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeMessageProducerFactory(_fakeMessageProducer)));
            };

            private Because _of_Handle = () => _repostHandler.Handle(_command);

            private It should_send_message_to_broker = () =>
            {
                _fakeMessageProducer.MessageWasSent.ShouldBeTrue();
            };

            private static string _storeName = "storeItemtestStoreName";
            private static RepostCommandHandler _repostHandler;
            private static RepostCommand _command;
            private static Message _messageToRepost;
            private static FakeMessageProducer _fakeMessageProducer;
        }

        public class When_reposting_messages_one_fails
        {
            private Establish _context = () =>
            {
                var fakeStore = new FakeMessageStoreWithViewer();
                _messageToRepost = new Message(new MessageHeader(Guid.NewGuid(), "a topic", MessageType.MT_COMMAND, DateTime.UtcNow), new MessageBody("body"));
                fakeStore.Add(_messageToRepost);
                _messageToRepostMissing = new Message(new MessageHeader(Guid.NewGuid(), "a topic", MessageType.MT_COMMAND, DateTime.UtcNow), new MessageBody("body"));
                var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);

                _command = new RepostCommand { MessageIds = new List<string> { _messageToRepost.Id.ToString(), _messageToRepostMissing.Id.ToString() }, StoreName = _storeName };
                _fakeMessageProducer = new FakeMessageProducer();
                _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeMessageProducerFactory(_fakeMessageProducer)));
            };

            private Because _of_Handle = () => _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            private It should_throw_expected_exception = () =>
            {
                _ex.ShouldBeOfExactType<SystemException>();
                _ex.Message.ShouldContain("messages");
                _ex.Message.ShouldContain(_messageToRepostMissing.Id.ToString());
            };

            private static string _storeName = "storeItemtestStoreName";
            private static RepostCommandHandler _repostHandler;
            private static RepostCommand _command;
            private static Message _messageToRepost;
            private static FakeMessageProducer _fakeMessageProducer;
            private static Exception _ex;
            private static Message _messageToRepostMissing;
        }

        public class When_reposting_messages_and_store_not_found
        {
            private Establish _context = () =>
            {
                var fakeMessageStoreFactory = FakeMessageStoreViewerFactory.CreateEmptyFactory();

                _command = new RepostCommand { MessageIds = new List<string> { Guid.NewGuid().ToString() }, StoreName = _storeName };
                _fakeMessageProducer = new FakeMessageProducer();
                _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeMessageProducerFactory(_fakeMessageProducer)));
            };

            private Because _of_Handle = () => _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            private It should_throw_expected_exception = () =>
            {
                _ex.ShouldBeOfExactType<SystemException>();
                _ex.Message.ShouldContain("Store");
            };

            private static string _storeName = "storeItemtestStoreName";
            private static RepostCommandHandler _repostHandler;
            private static RepostCommand _command;
            private static FakeMessageProducer _fakeMessageProducer;
            private static Exception _ex;
        }

        public class When_reposting_message_messages_not_found
        {
            private Establish _context = () =>
            {
                var fakeStore = new FakeMessageStoreWithViewer();
                var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);

                _command = new RepostCommand { MessageIds = new List<string> { Guid.NewGuid().ToString() }, StoreName = _storeName };
                _fakeMessageProducer = new FakeMessageProducer();
                _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeMessageProducerFactory(_fakeMessageProducer)));
            };

            private Because _of_Handle = () => _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            private It should_throw_expected_exception = () =>
            {
                _ex.ShouldBeOfExactType<SystemException>();
                _ex.Message.ShouldContain("messages");
                _ex.Message.ShouldContain(_command.MessageIds.Single());
            };

            private static string _storeName = "storeItemtestStoreName";
            private static RepostCommandHandler _repostHandler;
            private static RepostCommand _command;
            private static FakeMessageProducer _fakeMessageProducer;
            private static Exception _ex;
        }

        public class When_reposting_message_broker_not_found
        {
            private Establish _context = () =>
            {
                var fakeStore = new FakeMessageStoreWithViewer();
                _messageToRepost = new Message(new MessageHeader(Guid.NewGuid(), "a topic", MessageType.MT_COMMAND, DateTime.UtcNow), new MessageBody("body"));
                fakeStore.Add(_messageToRepost);
                var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);

                _command = new RepostCommand { MessageIds = new List<string> { _messageToRepost.Header.Id.ToString() }, StoreName = _storeName };
                _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(null));
            };

            private Because _of_Handle = () => _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            private It should_throw_expected_exception = () =>
            {
                _ex.ShouldBeOfExactType<SystemException>();
                _ex.Message.ShouldContain("Mis-configured");
            };

            private static string _storeName = "storeItemtestStoreName";
            private static RepostCommandHandler _repostHandler;
            private static RepostCommand _command;
            private static Message _messageToRepost;
            private static Exception _ex;
        }
        public class When_reposting_message_broker_cannot_be_created
        {
            private Establish _context = () =>
            {
                var fakeStore = new FakeMessageStoreWithViewer();
                _messageToRepost = new Message(new MessageHeader(Guid.NewGuid(), "a topic", MessageType.MT_COMMAND, DateTime.UtcNow), new MessageBody("body"));
                fakeStore.Add(_messageToRepost);
                var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);

                _command = new RepostCommand { MessageIds = new List<string> { _messageToRepost.Header.Id.ToString() }, StoreName = _storeName };
                _repostHandler = new RepostCommandHandler(fakeMessageStoreFactory, new FakeMessageProducerFactoryProvider(new FakeErrorProducingMessageProducerFactory()));
            };

            private Because _of_Handle = () => _ex = Catch.Exception(() => _repostHandler.Handle(_command));

            private It should_throw_expected_exception = () =>
            {
                _ex.ShouldBeOfExactType<SystemException>();
                _ex.Message.ShouldContain("Mis-configured");
            };

            private static string _storeName = "storeItemtestStoreName";
            private static RepostCommandHandler _repostHandler;
            private static RepostCommand _command;
            private static Message _messageToRepost;
            private static Exception _ex;
        }
    }

    internal class FakeMessageProducerFactory : IAmAMessageProducerFactory
    {
        private readonly IAmAMessageProducer _producer;

        public FakeMessageProducerFactory(IAmAMessageProducer producer)
        {
            _producer = producer;
        }

        public IAmAMessageProducer Create()
        {
            return _producer;
        }
    }

    internal class FakeMessageProducerFactoryProvider : IMessageProducerFactoryProvider
    {
        private readonly IAmAMessageProducerFactory _aFactory;

        public FakeMessageProducerFactoryProvider(IAmAMessageProducerFactory aFactory)
        {
            this._aFactory = aFactory;
        }

        public IAmAMessageProducerFactory Get(ILog logger)
        {
            return _aFactory;
        }
    }
    internal class FakeErrorProducingMessageProducerFactory : IAmAMessageProducerFactory
    {
        public IAmAMessageProducer Create()
        {
            throw new NotImplementedException();
        }
    }
}