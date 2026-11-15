#region Licence
/* The MIT License (MIT)
Copyright © 2026 Anurag Mishra <am6840957@gmail.com>

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

#nullable enable

using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Paramore.Brighter.MessagingGateway.AzureServiceBus;
using Xunit;

namespace Paramore.Brighter.AzureServiceBus.Tests.Configuration;

public class AzureSubscriptionPrefetchCountBindingTests
{
    [Fact]
    public void When_binding_service_bus_subscription_options_should_apply_prefetch_count()
    {
        // Arrange
        var options = new AzureServiceBusSubscriptionConfiguration();
        Assert.Equal(0, options.PrefetchCount);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PrefetchCount"] = "25"
        }).Build();

        // Act
        configuration.Bind(options);

        // Assert
        Assert.Equal(25, options.PrefetchCount);
        options.PrefetchCount = 0;
        Assert.Equal(0, options.PrefetchCount);
    }
}
