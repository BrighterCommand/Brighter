#region Licence
/* The MIT License (MIT)
Copyright � 2014 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using NUnit.Framework;
using Paramore.Brighter.Tests.TestDoubles;

namespace Paramore.Brighter.Tests
{
    [TestFixture]
    public class CommandProcessorNoMatchingSubcribersTests
    {
        private CommandProcessor _commandProcessor;
        private readonly MyEvent _myEvent = new MyEvent();
        private Exception _exception;

        [SetUp]
        public void Establish()
        {
            var registry = new SubscriberRegistry();
            var handlerFactory = new TestHandlerFactory<MyEvent, MyEventHandler>(() => new MyEventHandler());

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
        }

        [Test]
        public void When_There_Are_No_Subscribers()
        {
            _exception = Catch.Exception(() => _commandProcessor.Publish(_myEvent));

            //_should_not_throw_an_exception
            Assert.Null(_exception);
        }
    }
}