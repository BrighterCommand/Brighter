#region Licence

/* The MIT License (MIT)
Copyright © 2026 Avtandil Ushikishvili <a.ushikishvili@gmail.com>

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

namespace Paramore.Brighter.AzureServiceBus.Tests.Configuration;

public class AzureSubscriptionConfigurationBindingTests
{
    [Test]
    public async Task When_binding_service_bus_subscription_options_should_apply_filter_and_queue_selection()
    {
        //Arrange
        var options = new AzureServiceBusSubscriptionConfiguration();
        await Assert.That(options.SqlFilter).IsEqualTo(string.Empty);
        await Assert.That(options.UseServiceBusQueue).IsFalse();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SqlFilter"] = "priority > 5",
            ["UseServiceBusQueue"] = "true"
        }).Build();

        //Act
        configuration.Bind(options);

        //Assert
        await Assert.That(options.SqlFilter).IsEqualTo("priority > 5");
        await Assert.That(options.UseServiceBusQueue).IsTrue();
        options.SqlFilter = "priority > 10";
        options.UseServiceBusQueue = false;
        await Assert.That(options.SqlFilter).IsEqualTo("priority > 10");
        await Assert.That(options.UseServiceBusQueue).IsFalse();
    }
}
