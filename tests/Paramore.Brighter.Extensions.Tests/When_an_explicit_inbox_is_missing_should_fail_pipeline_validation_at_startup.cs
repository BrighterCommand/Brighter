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

namespace Paramore.Brighter.Extensions.Tests;

public class ConsumerMissingInboxStartupValidationTests
{
    [Test]
    [Arguments(false, false, false)]
    [Arguments(false, false, true)]
    [Arguments(false, true, false)]
    [Arguments(false, true, true)]
    [Arguments(true, false, false)]
    [Arguments(true, false, true)]
    [Arguments(true, true, false)]
    [Arguments(true, true, true)]
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
        var exception = await Assert.That(() => hostedService.StartAsync(CancellationToken.None)).ThrowsExactly<PipelineValidationException>();

        // Assert
        await Assert.That(exception.Message).Contains(nameof(InboxConfiguration));
        await Assert.That(exception.Message).Contains(useAsyncHandler
            ? nameof(ConsumerExplicitInboxAsyncCommandHandler)
            : nameof(ConsumerExplicitInboxCommandHandler));
    }
}
