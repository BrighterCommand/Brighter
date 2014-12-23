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
using Common.Logging;
using FakeItEasy;
using Machine.Specifications;
using paramore.brighter.commandprocessor;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.commandprocessor.tests.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Handler_For_A_Command
    {
        private static PipelineBuilder<MyCommand> Chain_Builder;
        private static IHandleRequests<MyCommand> Chain_Of_Responsibility;
        private static RequestContext request_context ;

        private Establish context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyCommandHandler>(() => new MyCommandHandler(logger));
            request_context = new RequestContext();

            Chain_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        Because of = () => Chain_Of_Responsibility = Chain_Builder.Build(request_context).First();

        It should_have_set_the_context_on_the_handler = () => Chain_Of_Responsibility.Context.ShouldNotBeNull();
        It should_use_the_context_that_we_passed_in = () => Chain_Of_Responsibility.Context.ShouldBeTheSameAs(request_context);
    }

    [Subject(typeof(PipelineBuilder<>))]
    public class When_putting_a_variable_into_the_bag_should_be_accessible_in_the_handler
    {
        private const string I_AM_A_TEST_OF_THE_CONTEXT_BAG = "I am a test of the context bag";
        private static RequestContext request_context;
        private static CommandProcessor commandProcessor;
        private static MyCommand myCommand;

        Establish context = () =>
        {
            var logger = A.Fake<ILog>();

            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyContextAwareCommandHandler>();
            var handlerFactory = new TestHandlerFactory<MyCommand, MyContextAwareCommandHandler>(() => new MyContextAwareCommandHandler(logger));
            request_context = new RequestContext();
            myCommand = new MyCommand();
            MyContextAwareCommandHandler.TestString = null;

            var requestContextFactory = A.Fake<IAmARequestContextFactory>();
            A.CallTo(() => requestContextFactory.Create()).Returns(request_context);

            commandProcessor = new CommandProcessor(registry, handlerFactory, requestContextFactory, new PolicyRegistry(),  logger);

            request_context.Bag["TestString"] = I_AM_A_TEST_OF_THE_CONTEXT_BAG;
        };


        Because of = () => commandProcessor.Send(myCommand);

        It should_have_seen_the_data_we_pushed_into_the_bag = () => MyContextAwareCommandHandler.TestString.ShouldEqual(I_AM_A_TEST_OF_THE_CONTEXT_BAG);
        It should_have_been_filled_by_the_handler = () => ((string)request_context.Bag["MyContextAwareCommandHandler"]).ShouldEqual("I was called and set the context");
    }


}
