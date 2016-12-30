#region Licence

/* The MIT License (MIT)
Copyright © 2014 Francesco Pighi <francesco.pighi@gmail.com>

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
using nUnitShouldAdapter;
using NUnit.Specifications;

namespace paramore.brighter.commandprocessor.tests.nunit.MessageStore.NoOpStore
{
    [Subject(typeof(NoOpMessageStore))]
    public class When_reading_from_noopstore : ContextSpecification
    {
        private static Message s_messageEarliest;
        private static NoOpMessageStore s_noOpStore;
        private static Exception s_exception;
        private static IList<Message> s_messages;

        private Establish _context = () =>
        {
            s_noOpStore = new NoOpMessageStore();
            s_messageEarliest = new Message(new MessageHeader(Guid.NewGuid(), "test_topic", MessageType.MT_DOCUMENT), new MessageBody("message body"));
            s_noOpStore.Add(s_messageEarliest);
        };

        private Because _of = () => { s_exception = Catch.Exception(() => s_messages = s_noOpStore.Get()); };

        private It _should_not_cause_exception = () => s_exception.ShouldBeNull();
        private It _should_return_empty_list = () =>
        {
            s_messages.ShouldNotBeNull();
            s_messages.Any().ShouldBeFalse();
        };
    }
}