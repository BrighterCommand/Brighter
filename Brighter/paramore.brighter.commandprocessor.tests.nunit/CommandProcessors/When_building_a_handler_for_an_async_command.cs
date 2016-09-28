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
using nUnitShouldAdapter;
using NUnit.Specifications;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    [Subject(typeof(PipelineBuilder<>))]
    public class When_Building_A_Handler_For_An_Async_Command : NUnit.Specifications.ContextSpecification
    {
        private static PipelineBuilder<MyCommand> s_chain_Builder;
        private static IHandleRequestsAsync<MyCommand> s_chain_Of_Responsibility;
        private static RequestContext s_request_context;

        private Establish _context = () =>
        {
            var logger = A.Fake<ILog>();
            var registry = new SubscriberRegistry();
            registry.RegisterAsync<MyCommand, MyCommandHandlerAsync>();
            var handlerFactory = new TestHandlerFactoryAsync<MyCommand, MyCommandHandlerAsync>(() => new MyCommandHandlerAsync(logger));
            s_request_context = new RequestContext();

            s_chain_Builder = new PipelineBuilder<MyCommand>(registry, handlerFactory, logger);
        };

        private Because _of = () => s_chain_Of_Responsibility = s_chain_Builder.BuildAsync(s_request_context, false).First();

        private It _should_have_set_the_context_on_the_handler = () => s_chain_Of_Responsibility.Context.ShouldNotBeNull();
        private It _should_use_the_context_that_we_passed_in = () => s_chain_Of_Responsibility.Context.ShouldBeTheSameAs(s_request_context);
    }
}
