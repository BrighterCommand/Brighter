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
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests.MessagingGateway;

public class SqsRequeueDelayValidationTests
{
    public static IEnumerable<object[]> InvalidDelays()
    {
        foreach (var delay in new[]
                 {
                     TimeSpan.FromTicks(-1), TimeSpan.FromSeconds(-1),
                     TimeSpan.FromHours(12).Add(TimeSpan.FromTicks(1)),
                     TimeSpan.FromSeconds(43201), TimeSpan.MaxValue, TimeSpan.MinValue
                 })
        {
            yield return [delay, false];
            yield return [delay, true];
        }
    }

    [Theory]
    [MemberData(nameof(InvalidDelays))]
    public async Task When_requeue_delay_is_out_of_range_should_reject_it(TimeSpan delay, bool useAsync)
    {
        // Arrange
        var connection = new AWSMessagingGatewayConnection(
            new BasicAWSCredentials("test", "test"), RegionEndpoint.USEast1,
            config =>
            {
                config.ServiceURL = "http://127.0.0.1:1";
                config.MaxErrorRetry = 0;
                config.Timeout = TimeSpan.FromSeconds(1);
            });
        using var consumer = new SqsMessageConsumer(
            connection, "http://127.0.0.1:1/000000000000/unused", isQueueUrl: true);
        var message = new Message();
        message.Header.Bag["ReceiptHandle"] = "receipt";

        // Act
        var exception = useAsync
            ? await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => consumer.RequeueAsync(message, delay))
            : Assert.Throws<ArgumentOutOfRangeException>(() => consumer.Requeue(message, delay));

        // Assert
        Assert.Equal("delay", exception.ParamName);
    }
}
