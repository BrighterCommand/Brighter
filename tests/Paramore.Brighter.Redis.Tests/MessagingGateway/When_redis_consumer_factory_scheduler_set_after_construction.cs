#region Licence
/* The MIT License (MIT)

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

using Paramore.Brighter.MessagingGateway.Redis;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

public class When_redis_consumer_factory_scheduler_set_after_construction
{
    private readonly RedisMessagingGatewayConfiguration _configuration = new()
    {
        RedisConnectionString = "localhost:6379",
        MaxPoolSize = 10
    };

    [Fact]
    public void Should_expose_scheduler_set_after_construction()
    {
        // Arrange — factory constructed without a scheduler
        var factory = new RedisMessageConsumerFactory(_configuration);
        var scheduler = new StubMessageScheduler();

        // Act — set scheduler after construction
        factory.Scheduler = scheduler;

        // Assert — scheduler property reflects the updated value
        Assert.Same(scheduler, factory.Scheduler);
    }

    [Fact]
    public void Should_use_constructor_scheduler_when_property_not_set()
    {
        // Arrange — factory constructed with a scheduler via constructor
        var scheduler = new StubMessageScheduler();
        var factory = new RedisMessageConsumerFactory(_configuration, scheduler);

        // Assert — scheduler property reflects the constructor value
        Assert.Same(scheduler, factory.Scheduler);
    }

    [Fact]
    public void Should_override_constructor_scheduler_with_property()
    {
        // Arrange — factory constructed with one scheduler
        var originalScheduler = new StubMessageScheduler();
        var factory = new RedisMessageConsumerFactory(_configuration, originalScheduler);

        // Act — override with a different scheduler
        var overrideScheduler = new StubMessageScheduler();
        factory.Scheduler = overrideScheduler;

        // Assert — property reflects the override, not the original
        Assert.Same(overrideScheduler, factory.Scheduler);
        Assert.NotSame(originalScheduler, factory.Scheduler);
    }

    private class StubMessageScheduler : IAmAMessageScheduler;
}
