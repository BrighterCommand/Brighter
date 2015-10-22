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

using System.Linq;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.brighter.commandprocessor.Logging;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Handler_For_A_Command
    {
        private static PipelineBuilder<MyCommand> s_chain_Builder;
        private static IHandleRequests<MyCommand> s_chain_Of_Responsibility;
        private static RequestContext s_request_context;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyCommandHandler>(() => new MyCommandHandler(logger));
            s_request_context = new RequestContext();

            s_chain_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_chain_Of_Responsibility = s_chain_Builder.Build(s_request_context).First();

        private It _should_have_set_the_context_on_the_handler = () => s_chain_Of_Responsibility.Context.ShouldNotBeNull();
        private It _should_use_the_context_that_we_passed_in = () => s_chain_Of_Responsibility.Context.ShouldBeTheSameAs(s_request_context);
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_putting_a_variable_into_the_bag_should_be_accessible_in_the_handler
    {
        private const string I_AM_A_TEST_OF_THE_CONTEXT_BAG = "I am a test of the context bag";
        private static RequestContext s_request_context;
        private static CommandProcessor s_commandProcessor;
        private static MyCommand s_myCommand;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyContextAwareCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyContextAwareCommandHandler>(() => new MyContextAwareCommandHandler());
            s_request_context = new RequestContext();
            s_myCommand = new MyCommand();
            MyContextAwareCommandHandler.TestString = null;

            var requestContextFactory = A.Fake<IAmARequestContextFactory>();
            A.CallTo(() => requestContextFactory.Create()).Returns(s_request_context);

            s_commandProcessor = new CommandProcessor(registry, handlerFactory, requestContextFactory, new PolicyRegistry(), logger);

            s_request_context.Bag["TestString"] = I_AM_A_TEST_OF_THE_CONTEXT_BAG;
        };


        private Because _of = () => s_commandProcessor.Send(s_myCommand);

        private It _should_have_seen_the_data_we_pushed_into_the_bag = () => MyContextAwareCommandHandler.TestString.ShouldEqual(I_AM_A_TEST_OF_THE_CONTEXT_BAG);
        private It _should_have_been_filled_by_the_handler = () => ((string)s_request_context.Bag["MyContextAwareCommandHandler"]).ShouldEqual("I was called and set the context");
    }
}
