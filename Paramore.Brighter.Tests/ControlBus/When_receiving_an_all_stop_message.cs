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

using FakeItEasy;
using NUnit.Framework;
using Paramore.Brighter.ServiceActivator;
using Paramore.Brighter.ServiceActivator.Ports.Commands;
using Paramore.Brighter.ServiceActivator.Ports.Handlers;

namespace Paramore.Brighter.Tests.ControlBus
{
    [TestFixture]
    public class ConfigurationCommandAllStopTests
    {
        private ConfigurationCommandHandler _configurationCommandHandler;
        private ConfigurationCommand _configurationCommand;
        private IDispatcher _dispatcher;

        [SetUp]
        public void Establish()
        {
            _dispatcher = A.Fake<IDispatcher>();
            _configurationCommandHandler = new ConfigurationCommandHandler(_dispatcher);
            _configurationCommand = new ConfigurationCommand(ConfigurationCommandType.CM_STOPALL);
        }

        [Test]
        public void When_receiving_an_all_stop_message()
        {
            _configurationCommandHandler.Handle(_configurationCommand);

            //_should_call_end_on_the_dispatcher
            A.CallTo(() => _dispatcher.End()).MustHaveHappened();
        }
    }
}