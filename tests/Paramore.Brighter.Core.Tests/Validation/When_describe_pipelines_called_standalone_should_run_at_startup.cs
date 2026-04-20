#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class DescribePipelinesStandaloneTests
{
    [Fact]
    public void When_describe_pipelines_called_should_register_diagnostic_hosted_service()
    {
        // Arrange
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);

        // Act
        builder.DescribePipelines();

        // Assert — a diagnostic hosted service is registered
        Assert.Contains(services, sd =>
            sd.ServiceType == typeof(IHostedService)
            && sd.ImplementationType?.Name == "BrighterDiagnosticHostedService");
    }

    [Fact]
    public async Task When_describe_pipelines_standalone_should_produce_log_output_at_startup()
    {
        // Arrange — DescribePipelines without ValidatePipelines, real diagnostic writer with captured logs
        var services = new ServiceCollection();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        subscriberRegistry.Register<MyDescribableCommand, MyPublicSyncHandler>();
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        builder.DescribePipelines();

        // Use a capturing logger factory so we can verify log output
        var logEntries = new List<LogEntry>();
        services.AddSingleton<ILoggerFactory>(new CapturingLoggerFactory(logEntries));

        var provider = services.BuildServiceProvider();

        // Act — start all hosted services (simulates host startup)
        var hostedServices = provider.GetServices<IHostedService>().ToList();
        foreach (var svc in hostedServices)
        {
            await svc.StartAsync(CancellationToken.None);
        }

        // Assert — the diagnostic writer ran and produced the pipeline summary log
        Assert.Contains(logEntries, e =>
            e.LogLevel == LogLevel.Information
            && e.Message.Contains("handler pipeline"));
    }

    /// <summary>
    /// A logger factory that captures all log entries for test assertions.
    /// </summary>
    private class CapturingLoggerFactory(List<LogEntry> entries) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries);
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    private class CapturingLogger(List<LogEntry> entries) : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new LogEntry(logLevel, formatter(state, exception)));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    }
}
