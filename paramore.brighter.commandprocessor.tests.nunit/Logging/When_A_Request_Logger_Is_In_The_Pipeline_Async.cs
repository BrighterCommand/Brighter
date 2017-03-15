using System;
using System.Collections.Generic;
using System.Linq;
using Nito.AsyncEx;
using NUnit.Framework;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Logging.Handlers;
using Paramore.Brighter.Tests.Logging.TestDoubles;
using Paramore.Brighter.Tests.TestDoubles;
using TinyIoC;

namespace Paramore.Brighter.Tests.Logging
{
   [TestFixture]
    public class CommandProcessorWithLoggingInPipelineAsyncTests
    {
        private SpyLog _logger;
        private MyCommand _myCommand;
        private IAmACommandProcessor _commandProcessor;

        [SetUp]
        public void Establish()
        {
            _logger = new SpyLog();
            _myCommand = new MyCommand();

            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyLoggedHandlerAsync>();

            var container = new TinyIoCContainer();
            container.Register<IHandleRequestsAsync<MyCommand>, MyLoggedHandlerAsync>();
            container.Register<IHandleRequestsAsync<MyCommand>, RequestLoggingHandlerAsync<MyCommand>>();

            var handlerFactory = new TinyIocHandlerFactoryAsync(container);

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());

            LogProvider.SetCurrentLogProvider(new SpyLogProvider(_logger));
        }

        [Test]
        public void When_A_Request_Logger_Is_In_The_Pipeline_Async()
        {
            AsyncContext.Run(async () => await _commandProcessor.SendAsync(_myCommand));


            //_should_log_the_request_handler_call
            Assert.True(((Func<IList<SpyLog.LogRecord>, bool>) (logs => logs.Any(log => log.Message.Contains("Logging handler pipeline call")))).Invoke(_logger.Logs));
            //_should_log_the_type_of_handler_in_the_call
            Assert.True(((Func<IList<SpyLog.LogRecord>, bool>) (logs => logs.Any(log => log.Message.Contains(typeof(MyCommand).ToString())))).Invoke(_logger.Logs));
        }

    }
}
