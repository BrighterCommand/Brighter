#region Licence
/* The MIT License (MIT)
Copyright © 2026 Irakli Gabisonia

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

#endregion

using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.Observability;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class CommandProcessorBuilderWithoutInboxTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void When_building_without_an_inbox_should_allow_duplicate_commands(bool useParameterlessOverload)
    {
        // Arrange
        var registry = new SubscriberRegistry();
        registry.Register<ConsumerGlobalInboxCommand, ConsumerGlobalInboxCommandHandler>();
        var handlerFactory = new SimpleHandlerFactorySync(_ => new ConsumerGlobalInboxCommandHandler());
        var handlerBuilder = useParameterlessOverload
            ? CommandProcessorBuilder.StartNew()
            : CommandProcessorBuilder.StartNew(null);
        var processor = handlerBuilder
            .Handlers(new HandlerConfiguration(registry, handlerFactory))
            .DefaultResilience()
            .NoExternalBus()
            .ConfigureInstrumentation(null, InstrumentationOptions.None)
            .RequestContextFactory(new InMemoryRequestContextFactory())
            .RequestSchedulerFactory(new InMemorySchedulerFactory())
            .Build();
        var command = new ConsumerGlobalInboxCommand();

        // Act
        processor.Send(command);
        processor.Send(command);

        // Assert
        Assert.Equal(2, command.HandleCount);
    }
}
