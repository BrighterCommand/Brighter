using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Paramore.Brighter.AWS.Tests.Helpers;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Xunit;

namespace Paramore.Brighter.AWS.Tests;

/// <summary>
/// The reaper runs from teardown, after a test that may already have failed, so a failure of its
/// own must not replace that result with a different one. Its per-resource deletes are guarded,
/// but the work around them — creating the SNS and SQS clients, and draining what it tracked —
/// is not.
/// </summary>
[Trait("Category", "AWS")]
public class AwsTestResourceReaperTeardownFailureTests
{
    private readonly AwsTestResourceReaper _reaper;

    public AwsTestResourceReaperTeardownFailureTests()
    {
        //arrange
        // Client configuration that fails stands in for any failure outside the per-resource
        // delete; AWSClientFactory applies this action while creating each client.
        var connection = new AWSMessagingGatewayConnection(
            new BasicAWSCredentials("test", "test"),
            RegionEndpoint.EUWest1,
            _ => throw new AmazonClientException("cannot configure a client"));

        _reaper = new AwsTestResourceReaper(connection);
        _reaper.TrackTopic("sns-std-0f8b1c2d3e4f5a6b7c8d9e0f1a2b3c4d");
        _reaper.TrackQueue("sqs-std-ch-0f8b1c2d3e4f5a6b7c8d9e0f1a2b3c4d");
    }

    [Fact]
    public async Task When_reaping_cannot_create_an_aws_client_should_not_throw()
    {
        //act
        var exception = await Catch.ExceptionAsync(() => _reaper.ReapAsync());

        //assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task When_reaping_cannot_create_an_aws_client_should_not_leave_resources_pending()
    {
        //act
        await _reaper.ReapAsync();

        //assert
        // Reaping is a single attempt: a run that could not delete does not leave the names
        // behind for a later call to try again.
        Assert.Empty(_reaper.PendingTopics);
        Assert.Empty(_reaper.PendingQueues);
    }
}
