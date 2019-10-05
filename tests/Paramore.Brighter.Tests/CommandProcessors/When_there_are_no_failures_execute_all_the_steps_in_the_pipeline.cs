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

using Confluent.Kafka;
using FluentAssertions;
using Paramore.Brighter.Tests.CommandProcessors.TestDoubles;
using Polly.Registry;
using TinyIoC;
using Xunit;

namespace Paramore.Brighter.Tests.CommandProcessors
{
    public class CommandProcessorPipelineStepsTests
    {
        private readonly CommandProcessor _commandProcessor;
        private readonly MyCommand _myCommand = new MyCommand();

        public CommandProcessorPipelineStepsTests()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyStepsPreAndPostDecoratedHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyStepsPreAndPostDecoratedHandler>();
            container.Register<IHandleRequests<MyCommand>, MyStepsValidationHandler<MyCommand>>();
            container.Register<IHandleRequests<MyCommand>, MyStepsLoggingHandler<MyCommand>>();

            _commandProcessor = new CommandProcessor(registry, handlerFactory, new InMemoryRequestContextFactory(), new PolicyRegistry());
            PipelineBuilder<MyCommand>.ClearPipelineCache();
        }

        [Fact]
        public void When_There_Are_No_Failures_Execute_All_The_Steps_In_The_Pipeline()
        {
            _commandProcessor.Send(_myCommand);


            //_should_call_the_pre_validation_handler
            MyStepsValidationHandler<MyCommand>.ShouldReceive(_myCommand).Should().BeTrue();
            //_should_send_the_command_to_the_command_handler
            MyStepsPreAndPostDecoratedHandler.ShouldReceive(_myCommand).Should().BeTrue();
            // _should_call_the_post_validation_handler
            MyStepsLoggingHandler<MyCommand>.Shouldreceive(_myCommand).Should().BeTrue();
        }
    }
}
