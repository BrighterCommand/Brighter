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

using System.Linq;
using nUnitShouldAdapter;
using NUnit.Framework;
using paramore.brighter.commandprocessor.tests.nunit.CommandProcessors.TestDoubles;
using TinyIoC;

namespace paramore.brighter.commandprocessor.tests.nunit.CommandProcessors
{
    [TestFixture]
    public class PipelineOrderingTests
    {
        private PipelineBuilder<MyCommand> _pipelineBuilder;
        private IHandleRequests<MyCommand> _pipeline;

        [SetUp]
        public void Establish()
        {
            var registry = new SubscriberRegistry();
            registry.Register<MyCommand, MyDoubleDecoratedHandler>();

            var container = new TinyIoCContainer();
            var handlerFactory = new TinyIocHandlerFactory(container);
            container.Register<IHandleRequests<MyCommand>, MyDoubleDecoratedHandler>();
            container.Register<IHandleRequests<MyCommand>, MyValidationHandler<MyCommand>>();
            container.Register<IHandleRequests<MyCommand>, MyLoggingHandler<MyCommand>>();

            _pipelineBuilder = new PipelineBuilder<MyCommand>(registry, handlerFactory);
        }

        [Test]
        public void When_Building_A_Pipeline_Preserve_The_Order()
        {
            _pipeline = _pipelineBuilder.Build(new RequestContext()).First();

            PipelineTracer().ToString().ShouldEqual("MyLoggingHandler`1|MyValidationHandler`1|MyDoubleDecoratedHandler|");
        }

        private PipelineTracer PipelineTracer()
        {
            var pipelineTracer = new PipelineTracer();
            _pipeline.DescribePath(pipelineTracer);
            return pipelineTracer;
        }
    }
}