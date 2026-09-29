#region Licence
/* The MIT License (MIT)
Copyright © 2026 Tom Longhurst

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

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ExplicitLoggingConfigurationTests
{
    [Fact]
    public void When_command_processor_builder_has_no_logger_factory_should_fail_with_guidance()
    {
        // Arrange
        var builder = (CommandProcessorBuilder)CommandProcessorBuilder.StartNew();
        // Act
        var exception = Assert.Throws<ConfigurationException>(() => builder.Build());
        // Assert
        Assert.Contains("ConfigureLogging", exception.Message);
    }

    [Fact]
    public void When_dispatch_builder_has_no_logger_factory_should_fail_with_guidance()
    {
        // Arrange
        var builder = (DispatchBuilder)DispatchBuilder.StartNew();
        // Act
        var exception = Assert.Throws<ConfigurationException>(() => builder.Build());
        // Assert
        Assert.Contains("ConfigureLogging", exception.Message);
    }

    [Fact]
    public void When_dependency_injection_has_no_logger_factory_should_explain_registration()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddBrighter();
        using var provider = services.BuildServiceProvider();
        // Act
        var exception = Assert.Throws<ConfigurationException>(() => provider.GetRequiredService<IAmACommandProcessor>());
        // Assert
        Assert.Contains("AddLogging", exception.Message);
        Assert.Contains("NullLoggerFactory", exception.Message);
    }

    [Fact]
    public void When_a_manual_handler_chain_is_configured_should_forward_the_request()
    {
        // Arrange
        var first = new ConsumerGlobalInboxCommandHandler();
        var second = new ConsumerGlobalInboxCommandHandler();
        first.ConfigureLogging(NullLoggerFactory.Instance);
        second.ConfigureLogging(NullLoggerFactory.Instance);
        first.SetSuccessor(second);
        var command = new ConsumerGlobalInboxCommand();
        // Act
        first.Handle(command);
        // Assert
        Assert.Equal(2, command.HandleCount);
    }

    [Fact]
    public void When_a_manual_handler_chain_has_no_logger_should_explain_configuration()
    {
        // Arrange
        var handler = new ConsumerGlobalInboxCommandHandler();
        handler.SetSuccessor(new ConsumerGlobalInboxCommandHandler());
        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => handler.Handle(new ConsumerGlobalInboxCommand()));
        // Assert
        Assert.Contains("ConfigureLogging", exception.Message);
    }

    [Fact]
    public void When_a_handler_receives_a_null_logger_factory_should_name_the_argument()
    {
        // Arrange
        var handler = new ConsumerGlobalInboxCommandHandler();
        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => handler.ConfigureLogging(null!));
        // Assert
        Assert.Equal("loggerFactory", exception.ParamName);
    }
}
