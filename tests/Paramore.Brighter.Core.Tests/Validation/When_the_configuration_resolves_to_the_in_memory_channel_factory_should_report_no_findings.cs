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
using System.Linq;
using Paramore.Brighter.Core.Tests.Validation.TestDoubles;
using Paramore.Brighter.ServiceActivator.Validation;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Validation;

public class DefaultInMemoryConfigurationIsSilentValidationTests
{
    [Fact]
    public void When_the_configuration_resolves_to_the_in_memory_channel_factory_should_report_no_findings()
    {
        // Arrange — a plain subscription, no default channel factory configured at all
        var subscription = new Subscription<FakeChannelFactoryRequest>();

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(null);

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — no findings
        Assert.True(satisfied);
        Assert.Empty(results);
    }

    [Fact]
    public void When_the_configuration_explicitly_uses_the_in_memory_channel_factory_should_report_no_findings()
    {
        // Arrange — a plain subscription, default channel factory explicitly an InMemoryChannelFactory
        var subscription = new Subscription<FakeChannelFactoryRequest>();
        var defaultChannelFactory = new InMemoryChannelFactory(new InternalBus(), TimeProvider.System, loggerFactory: Initializer.TestLoggerFactory);

        var spec = ConsumerValidationRules.ChannelFactoryCompatible(defaultChannelFactory);

        // Act
        var satisfied = spec.IsSatisfiedBy(subscription);
        var collector = new ValidationResultCollector<Subscription>();
        var results = spec.Accept(collector).ToList();

        // Assert — no findings
        Assert.True(satisfied);
        Assert.Empty(results);
    }
}
