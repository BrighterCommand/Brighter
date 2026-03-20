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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter.Extensions.DependencyInjection;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class ValidatePipelinesWithoutDescribeTests
{
    [Fact]
    public async Task When_validate_pipelines_called_without_describe_should_build_and_start()
    {
        // Arrange — register ValidatePipelines but NOT DescribePipelines
        var services = new ServiceCollection();
        services.AddLogging();
        var subscriberRegistry = new ServiceCollectionSubscriberRegistry(services);
        services.AddSingleton(subscriberRegistry);
        var mapperRegistry = new ServiceCollectionMessageMapperRegistryBuilder(services);
        var builder = new ServiceCollectionBrighterBuilder(services, subscriberRegistry, mapperRegistry);

        builder.ValidatePipelines();

        // Act — building the service provider and resolving the hosted service should not throw
        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>();
        var validationService = hostedServices.FirstOrDefault(s => s.GetType().Name == "BrighterValidationHostedService");

        Assert.NotNull(validationService);

        // StartAsync should complete without throwing (no diagnostic writer, no errors)
        await validationService.StartAsync(CancellationToken.None);
    }
}
