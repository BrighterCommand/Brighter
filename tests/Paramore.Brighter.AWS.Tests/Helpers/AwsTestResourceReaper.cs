using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.SecurityToken.Model;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using Amazon.SQS.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS;

namespace Paramore.Brighter.AWS.Tests.Helpers;

/// <summary>
/// Records the SNS topics and SQS queues a test fixture asks for, so that teardown can delete
/// them again.
/// </summary>
/// <remarks>
/// The gateway tags what it creates with Source=Brighter, but nothing marks a resource as
/// belonging to a particular test, so anything a fixture does not delete itself survives until
/// someone sweeps the account by hand. Fixtures pass every name they generate through
/// <see cref="TrackTopic"/> or <see cref="TrackQueue"/> and call <see cref="ReapAsync"/> from
/// teardown.
///
/// Names are reaped rather than the objects built from them. A test that fails while standing
/// its infrastructure up has still created the topic or queue, and by that point there is no
/// producer or channel left to dispose; deleting by name also catches resources created as a
/// side effect of a name, such as a dead letter queue or a topic auto-created by a producer.
/// </remarks>
public class AwsTestResourceReaper
{
    private readonly AWSMessagingGatewayConnection _connection;
    private readonly List<string> _topics = [];
    private readonly List<string> _queues = [];
    private string? _accountId;
    private bool _identityUnavailable;

    public AwsTestResourceReaper(AWSMessagingGatewayConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Registers an SNS topic name for deletion, returning it unchanged so that it can be
    /// tracked inline where it is generated.
    /// </summary>
    public string TrackTopic(string name)
    {
        _topics.Add(name);
        return name;
    }

    /// <summary>
    /// Registers an SQS queue name for deletion, returning it unchanged so that it can be
    /// tracked inline where it is generated.
    /// </summary>
    public string TrackQueue(string name)
    {
        _queues.Add(name);
        return name;
    }

    /// <summary>
    /// The topic names tracked but not yet reaped. Empty once <see cref="ReapAsync"/> has run.
    /// </summary>
    public IReadOnlyCollection<string> PendingTopics => _topics;

    /// <summary>
    /// The queue names tracked but not yet reaped. Empty once <see cref="ReapAsync"/> has run.
    /// </summary>
    public IReadOnlyCollection<string> PendingQueues => _queues;

    /// <summary>
    /// Deletes every tracked topic and queue. Failures are swallowed: teardown runs after a
    /// test that may already have failed, and must not replace that failure with its own.
    /// </summary>
    /// <remarks>
    /// Reaping is a single attempt. The tracked names are dropped whether or not the delete
    /// reached AWS, so a second call is a no-op rather than a retry; anything left behind by a
    /// failed sweep is the account sweep's problem, not teardown's.
    /// </remarks>
    public async Task ReapAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Topics first. Deleting a topic takes its subscriptions with it, which would
            // otherwise be left pointing at queues we are about to delete.
            await ReapTopicsAsync(cancellationToken);
            await ReapQueuesAsync(cancellationToken);
        }
        finally
        {
            _topics.Clear();
            _queues.Clear();
        }
    }

    private async Task ReapTopicsAsync(CancellationToken cancellationToken)
    {
        if (_topics.Count == 0)
        {
            return;
        }

        try
        {
            using var snsClient = new AWSClientFactory(_connection).CreateSnsClient();
            foreach (var topic in _topics)
            {
                await DeleteTopicAsync(snsClient, topic, cancellationToken);
            }
        }
        catch (Exception)
        {
            // Swallowed by design; see ReapAsync. Creating the client can fail as readily as
            // the deletes it is created for.
        }
    }

    private async Task ReapQueuesAsync(CancellationToken cancellationToken)
    {
        if (_queues.Count == 0)
        {
            return;
        }

        try
        {
            using var sqsClient = new AWSClientFactory(_connection).CreateSqsClient();
            foreach (var queue in _queues)
            {
                await DeleteQueueAsync(sqsClient, queue, cancellationToken);
            }
        }
        catch (Exception)
        {
            // Swallowed by design; see ReapAsync.
        }
    }

    /// <summary>
    /// Runs <see cref="ReapAsync"/> to completion on the calling thread, for fixtures that tear
    /// down synchronously.
    /// </summary>
    public void Reap() => ReapAsync().GetAwaiter().GetResult();

    private async Task DeleteTopicAsync(
        IAmazonSimpleNotificationService snsClient,
        string topicName,
        CancellationToken cancellationToken)
    {
        try
        {
            var topicArn = await ResolveTopicArnAsync(snsClient, topicName, cancellationToken);
            if (topicArn is null)
            {
                return;
            }

            // DeleteTopic is idempotent, so a topic the test never got as far as creating is not
            // an error.
            await snsClient.DeleteTopicAsync(topicArn, cancellationToken);
        }
        catch (Exception)
        {
            // Swallowed by design; see ReapAsync.
        }
    }

    private async Task DeleteQueueAsync(
        IAmazonSQS sqsClient,
        string queueName,
        CancellationToken cancellationToken)
    {
        try
        {
            var queueUrl = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
            await sqsClient.DeleteQueueAsync(queueUrl.QueueUrl, cancellationToken);
        }
        catch (QueueDoesNotExistException)
        {
            // The test failed before it created the queue, or already deleted it.
        }
        catch (Exception)
        {
            // Swallowed by design; see ReapAsync.
        }
    }

    /// <summary>
    /// Builds the ARN for a topic name from the caller's account, rather than searching for it.
    /// </summary>
    /// <remarks>
    /// <see cref="AmazonSimpleNotificationServiceClient.FindTopicAsync"/> pages through every
    /// topic in the account on each call, which turns teardown across a full test run into a
    /// quadratic scan. Only the account id has to be looked up — the region carries its own
    /// partition — so we ask STS once and compose ARNs the same way
    /// <see cref="ValidateTopicByArnConvention"/> does, falling back to a search if that lookup
    /// is unavailable.
    /// </remarks>
    private async Task<string?> ResolveTopicArnAsync(
        IAmazonSimpleNotificationService snsClient,
        string topicName,
        CancellationToken cancellationToken)
    {
        if (_accountId is null && !_identityUnavailable)
        {
            try
            {
                using var stsClient = new AWSClientFactory(_connection).CreateStsClient();
                var identity = await stsClient.GetCallerIdentityAsync(
                    new GetCallerIdentityRequest(), cancellationToken);

                _accountId = identity.Account;
            }
            catch (Exception)
            {
                // Asked once. Retrying per topic would add a failing call to each of the
                // searches the lookup exists to avoid.
                _identityUnavailable = true;
            }
        }

        if (_accountId is null)
        {
            // FindTopicAsync has no cancellable overload in this SDK.
            return (await snsClient.FindTopicAsync(topicName))?.TopicArn;
        }

        return new Arn
        {
            Partition = _connection.Region.PartitionName,
            Service = "sns",
            Region = _connection.Region.SystemName,
            AccountId = _accountId,
            Resource = topicName
        }.ToString();
    }
}
