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

using Paramore.Brighter.MessagingGateway.Redis;
using Paramore.Brighter.Redis.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Redis.Tests.MessagingGateway;

[Collection("Redis Shared Pool")]
public class When_multiple_redis_gateways_each_owns_its_own_pool
{
    private static readonly RedisMessagingGatewayConfiguration s_configuration = new()
    {
        RedisConnectionString = "localhost:6379",
        MaxPoolSize = 10
    };

    [Fact]
    public void Each_gateway_instance_should_have_a_distinct_pool()
    {
        using var first = new TestRedisGateway(s_configuration, new RoutingKey("topic-1"));
        using var second = new TestRedisGateway(s_configuration, new RoutingKey("topic-2"));

        Assert.NotSame(first.ExposedPool, second.ExposedPool);
    }
}
