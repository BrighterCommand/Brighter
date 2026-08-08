#region Licence
/* The MIT License (MIT)
Copyright © 2026 Miguel Ramirez <xbizzybone@gmail.com>

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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidatePipelinesNonBlockingWarningsTests
{
    [Fact]
    public async Task When_throw_on_error_true_with_transform_and_provider_triggers_should_not_block_and_surface_both_warnings()
    {
        // Arrange — an (A) producer trigger (publication whose custom mapper declares an unresolvable wrap
        // transform) AND a (B) trigger (a handler declaring a validation step with no provider), with
        // throwOnError: true. Both findings are Warnings, so startup must not be blocked.
        var routingKey = new RoutingKey("greeting");
        var producer = new InMemoryMessageProducer(
            new InternalBus(),
            new Publication { Topic = routingKey, RequestType = typeof(MyDescribableCommand) });
        var producerRegistry = new ProducerRegistry(
            new Dictionary<RoutingKey, IAmAMessageProducer> { { routingKey, producer } });

        var services = new ServiceCollection();
        services.AddLogging();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        subscriberRegistry.Register<MyValidatedCommand, MyValidatedSyncHandler>();
        services.AddSingleton(subscriberRegistry);
        services.AddSingleton<IAmAProducerRegistry>(producerRegistry);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        mapperRegistry.Register<MyDescribableCommand, MyDescribableCommandMessageMapper>();
        services.AddSingleton(mapperRegistry);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);
        builder.ValidatePipelines(throwOnError: true);

        var provider = services.BuildServiceProvider();

        // Act — starting the validation hosted service must not throw (warnings never block startup)
        var hostedService = provider.GetServices<IHostedService>().OfType<BrighterValidationHostedService>().Single();
        await hostedService.StartAsync(CancellationToken.None);

        var result = provider.GetRequiredService<IAmAPipelineValidator>().Validate();

        // Assert — the host started (no throw), result is valid (warnings only), and both warnings surfaced
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Message.Contains(nameof(MyDescribableTransform)));
        Assert.Contains(result.Warnings, w => w.Message.Contains("UseFluentValidation"));
    }
}
