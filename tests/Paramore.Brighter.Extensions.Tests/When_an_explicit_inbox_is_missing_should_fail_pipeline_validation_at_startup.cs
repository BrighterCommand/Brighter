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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Tests.TestDoubles;
using Paramore.Brighter.ServiceActivator.Extensions.DependencyInjection;
using Paramore.Brighter.ServiceActivator.Extensions.Hosting;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerMissingInboxStartupValidationTests
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
    public async Task When_an_explicit_inbox_is_missing_should_fail_pipeline_validation_at_startup(
        bool useOptionsFactory, bool useAsyncHandler, bool useIncompatibleInbox)
    {
        // Arrange
        var configuration = useIncompatibleInbox
            ? new InboxConfiguration(useAsyncHandler ? A.Fake<IAmAnInboxSync>() : A.Fake<IAmAnInboxAsync>())
            : null;
        var services = new ServiceCollection();
        IBrighterBuilder builder;
        if (useOptionsFactory)
            builder = services.AddConsumers(_ => new ConsumersOptions { InboxConfiguration = configuration });
        else
            builder = services.AddConsumers(options => { options.InboxConfiguration = configuration; });

        builder.ValidatePipelines(throwOnError: true);
        services.AddLogging();
        services.AddSingleton(Initializer.Factory);
        services.AddHostedService<ServiceActivatorHostedService>();
        using var provider = services.BuildServiceProvider();
        var hostedService = provider.GetServices<IHostedService>().OfType<ServiceActivatorHostedService>().Single();

        // Act
        var exception = await Assert.ThrowsAsync<PipelineValidationException>(
            () => hostedService.StartAsync(CancellationToken.None));

        // Assert
        Assert.Contains(nameof(InboxConfiguration), exception.Message);
        Assert.Contains(useAsyncHandler
            ? nameof(ConsumerExplicitInboxAsyncCommandHandler)
            : nameof(ConsumerExplicitInboxCommandHandler), exception.Message);
    }
}
