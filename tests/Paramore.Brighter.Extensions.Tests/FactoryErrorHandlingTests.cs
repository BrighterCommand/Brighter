#region Licence
/* The MIT License (MIT)
Copyright © 2024 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

/// <summary>
/// Tests that verify error handling in ServiceProviderHandlerFactory.
/// </summary>
public class FactoryErrorHandlingTests
{
    [Fact]
    public void Factory_UnregisteredHandler_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        // Note: NOT registering TestHandler
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope(factory.CreatePipelineScope());

        // Act
        var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(UnregisteredHandler), lifetime);

        // Assert - Should return null for unregistered handler
        Assert.Null(handler);
    }

    [Fact]
    public void Factory_NullLifetime_ThrowsConfigurationExceptionForTransient()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<SimpleHandler>();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);

        // Act & Assert - Transient now requires a pipeline scope handle too (C-6, see
        // ServiceProviderHandlerFactory.CreatePipelineScope), so a null lifetime carries none
        Assert.Throws<ConfigurationException>(() => ((IAmAHandlerFactorySync)factory).Create(typeof(SimpleHandler), null!));
    }

    [Fact]
    public void Factory_InvalidHandlerType_ReturnsNull()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IBrighterOptions>(new BrighterOptions
        {
            HandlerLifetime = ServiceLifetime.Transient
        });

        var provider = services.BuildServiceProvider();
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope(factory.CreatePipelineScope());

        // Act - Pass a non-handler type (string is not a handler)
        var result = ((IAmAHandlerFactorySync)factory).Create(typeof(string), lifetime);

        // Assert - Should return null for non-handler types
        Assert.Null(result);
    }

    [Fact]
    public void Factory_MissingBrighterOptions_UsesDefaultTransient()
    {
        // Arrange - Don't register IBrighterOptions
        var services = new ServiceCollection();
        services.AddTransient<SimpleHandler>();
        // NOT registering IBrighterOptions

        var provider = services.BuildServiceProvider();

        // Act - Factory should handle missing options gracefully
        var factory = new ServiceProviderHandlerFactory(provider);
        var lifetime = new TestLifetimeScope(factory.CreatePipelineScope());
        var handler = ((IAmAHandlerFactorySync)factory).Create(typeof(SimpleHandler), lifetime);

        // Assert - Should use default Transient lifetime and work
        Assert.NotNull(handler);
    }

    private class UnregisteredHandler : RequestHandler<ErrorCommand>
    {
        public override ErrorCommand Handle(ErrorCommand command) => command;
    }

    private class SimpleHandler : RequestHandler<ErrorCommand>
    {
        public override ErrorCommand Handle(ErrorCommand command) => command;
    }

    private class ErrorCommand : Command
    {
        public ErrorCommand() : base(Guid.NewGuid()) { }
    }

    private class TestLifetimeScope : IAmALifetime
    {
        public TestLifetimeScope(IAmAScope? pipelineScope = null) => PipelineScope = pipelineScope;
        public IAmAScope? PipelineScope { get; }
        public void Add(IHandleRequests instance) { }
        public void Add(IHandleRequestsAsync instance) { }
        public void Dispose() => PipelineScope?.Dispose();
    }
}
