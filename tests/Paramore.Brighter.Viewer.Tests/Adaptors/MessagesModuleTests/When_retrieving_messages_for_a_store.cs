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
using Nancy.Testing;
using NUnit.Framework;
using Paramore.Brighter.MessageViewer.Adaptors.API.Modules;
using Paramore.Brighter.MessageViewer.Adaptors.API.Resources;
using Paramore.Brighter.Viewer.Tests.TestDoubles;

namespace Paramore.Brighter.Viewer.Tests.Adaptors.MessagesModuleTests
{
    [TestFixture]
    public class RetrieveMessagesForStoreTests
    {
        private static string _storeName = "testStore";
        private readonly string _uri = string.Format("/messages/{0}", _storeName);
        private Browser _browser;
        private BrowserResponse _result;

        [Test]
        public void Establish()
        {
            var messages = new List<Message>
            {
                new Message(new MessageHeader(Guid.NewGuid(), "MyTopic1", MessageType.MT_COMMAND),
                    new MessageBody("")),
                new Message(new MessageHeader(Guid.NewGuid(), "MyTopic2", MessageType.MT_COMMAND),
                    new MessageBody(""))
            };

            _browser = new Browser(new ConfigurableBootstrapper(with =>
            {
                var messageListViewModelRetriever = new FakeMessageListViewModelRetriever(new MessageListModel(messages));
                with.Module(new MessagesNancyModule(messageListViewModelRetriever, new FakeHandlerFactory()));
            }));
        }


        [Test]
        public void When_retrieving_messages_for_a_store()
        {
            _result = _browser.Get(_uri, with =>
                {
                    with.Header("accept", "application/json");
                    with.HttpRequest();
                })
                .Result;

            //should_return_200_OK
            Assert.AreEqual(Nancy.HttpStatusCode.OK, _result.StatusCode);
            //should_return_json
            StringAssert.Contains("application/json", _result.ContentType);
            //should_return_MessageListModel
             var model = Newtonsoft.Json.JsonConvert.DeserializeObject<MessageListModel>(_result.Body.AsString());
            Assert.NotNull(model);
        }
   }
}