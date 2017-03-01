#region Licence

/* The MIT License (MIT)
Copyright � 2014 Francesco Pighi <francesco.pighi@gmail.com>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the �Software�), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED �AS IS�, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
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
using NUnit.Framework;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageStore.NoOpStore
{
    [TestFixture]
    public class NoOpMessageStoreReadTests
    {
        private Message _messageEarliest;
        private NoOpMessageStore _noOpStore;
        private Exception _exception;
        private IList<Message> _messages;

        [SetUp]
        public void Establish ()
        {
            _noOpStore = new NoOpMessageStore();
            _messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
            _noOpStore.Add(_messageEarliest);
        }

        [Test]
        public void When_reading_from_noopstore()
        {
            _exception = Catch.Exception(() => _messages = _noOpStore.Get());

            //_should_not_cause_exception
            _exception.ShouldBeNull();
            //_should_return_empty_list
            _messages.ShouldNotBeNull();
            _messages.Any().ShouldBeFalse();

        }
   }
}