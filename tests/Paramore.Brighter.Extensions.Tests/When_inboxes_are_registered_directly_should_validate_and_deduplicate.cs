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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class InboxServiceValidationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task When_inboxes_are_registered_directly_should_validate_and_deduplicate(
        bool useOptionsFactory, bool registerBeforeConsumers, bool overrideIncompatibleConfiguration)
    {
        // Arrange
        var services = new ServiceCollection();
        var inbox = new InMemoryInbox(TimeProvider.System);
        if (registerBeforeConsumers)
        {
            services.AddSingleton<IAmAnInboxSync>(inbox);
            services.AddSingleton<IAmAnInboxAsync>(inbox);
        }
        var configuration = overrideIncompatibleConfiguration ? new InboxConfiguration(A.Fake<IAmAnInbox>()) : null;
        var builder = useOptionsFactory
            ? services.AddConsumers(_ => new ConsumersOptions { InboxConfiguration = configuration })
            : services.AddConsumers(options => { options.InboxConfiguration = configuration; });
        if (!registerBeforeConsumers)
        {
            services.AddSingleton<IAmAnInboxSync>(inbox);
            services.AddSingleton<IAmAnInboxAsync>(inbox);
        }
        builder.ValidatePipelines();
        using var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IAmACommandProcessor>();
        var command = new ConsumerExplicitInboxCommand();
        var asyncCommand = new ConsumerExplicitInboxAsyncCommand();

        // Act
        processor.Send(command);
        processor.Send(command);
        await processor.SendAsync(asyncCommand);
        await processor.SendAsync(asyncCommand);
        var result = Validate(provider);

        // Assert
        Assert.Equal(1, command.HandleCount);
        Assert.Equal(1, asyncCommand.HandleCount);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void When_an_inbox_registration_is_invalid_should_report_validation_errors(
        bool useAsyncHandler, bool factoryThrows)
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = services.AddConsumers(_ => new ConsumersOptions { InboxConfiguration = new InboxConfiguration() });
        if (useAsyncHandler)
            services.AddSingleton<IAmAnInboxAsync>(_ => factoryThrows ? throw new InvalidOperationException("inbox construction failed") : null!);
        else
            services.AddSingleton<IAmAnInboxSync>(_ => factoryThrows ? throw new InvalidOperationException("inbox construction failed") : null!);
        builder.ValidatePipelines();
        using var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Source.Contains(useAsyncHandler ? nameof(ConsumerExplicitInboxAsyncCommandHandler) : nameof(ConsumerExplicitInboxCommandHandler))
            && error.Message.Contains(useAsyncHandler ? nameof(IAmAnInboxAsync) : nameof(IAmAnInboxSync)));
        if (factoryThrows)
            Assert.Contains(result.Errors, error => error.Message.Contains("inbox construction failed"));
    }

    [Fact]
    public void When_validating_scoped_inboxes_should_dispose_them_and_resolve_fresh_on_each_validation()
    {
        // Arrange
        var services = new ServiceCollection();
        var created = new List<ValidationInMemoryInbox>();
        var builder = services.AddConsumers(options =>
        {
            options.HandlerLifetime = ServiceLifetime.Scoped;
            options.MapperLifetime = ServiceLifetime.Scoped;
            options.TransformerLifetime = ServiceLifetime.Scoped;
        });
        services.AddScoped(_ =>
        {
            var inbox = new ValidationInMemoryInbox();
            created.Add(inbox);
            return inbox;
        });
        services.AddScoped<IAmAnInboxSync>(sp => sp.GetRequiredService<ValidationInMemoryInbox>());
        services.AddScoped<IAmAnInboxAsync>(sp => sp.GetRequiredService<ValidationInMemoryInbox>());
        builder.ValidatePipelines();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

        // Act
        var first = Validate(provider);
        var second = Validate(provider);

        // Assert
        Assert.Empty(first.Errors);
        Assert.Empty(second.Errors);
        Assert.Equal(2, created.Count);
        Assert.All(created, inbox => Assert.True(inbox.Disposed));
    }

    [Fact]
    public void When_no_handler_uses_an_inbox_should_not_resolve_inbox_services()
    {
        // Arrange
        var services = new ServiceCollection();
        var registry = new SubscriberRegistry();
        registry.Register<ConsumerGlobalInboxCommand, ConsumerGlobalInboxCommandHandler>();
        services.AddSingleton<IAmASubscriberRegistryInspector>(registry);
        var builder = services.AddConsumers(_ => new ConsumersOptions());
        var resolutions = 0;
        services.AddSingleton<IAmAnInboxSync>(_ =>
        {
            resolutions++;
            throw new InvalidOperationException("Unused inbox must not be resolved");
        });
        services.AddSingleton<IAmAnInboxAsync>(_ =>
        {
            resolutions++;
            throw new InvalidOperationException("Unused inbox must not be resolved");
        });
        builder.ValidatePipelines();
        using var provider = services.BuildServiceProvider();

        // Act
        var result = Validate(provider);

        // Assert
        Assert.Empty(result.Errors);
        Assert.Equal(0, resolutions);
    }

    private static PipelineValidationResult Validate(IServiceProvider provider)
        => PipelineValidationResult.Combine(provider.GetServices<IAmAPipelineValidator>().Select(v => v.Validate()).ToArray());
}
