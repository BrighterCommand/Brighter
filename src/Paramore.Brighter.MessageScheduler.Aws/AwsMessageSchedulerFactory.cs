using System.Net;
using Amazon;
using Amazon.IdentityManagement.Model;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS.Model;
using Paramore.Brighter.MessagingGateway.AWSSQS;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessageScheduler.Aws;

/// <summary>
/// The Aws message Scheduler factory
/// </summary>
public class AwsMessageSchedulerFactory(AWSMessagingGatewayConnection connection, string role)
    : IAmAMessageSchedulerFactory
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _roleArn;
    private string? _topicArn;
    private string? _queueUrl;

    /// <summary>
    /// The AWS Scheduler group
    /// </summary>
    public SchedulerGroup Group { get; set; } = new();

    /// <summary>
    /// Get or create a scheduler id
    /// </summary>
    public Func<Message, string> GetOrCreateSchedulerId { get; set; } = _ => Guid.NewGuid().ToString("N");

    /// <summary>
    /// The flexible time window
    /// </summary>
    public int? FlexibleTimeWindowMinutes { get; set; }

    /// <summary>
    /// The topic or queue that Brighter should use for publishing/sending messaging scheduler
    /// It can be Topic Name/ARN or Queue Name/Url
    /// </summary>
    public RoutingKey SchedulerTopicOrQueue { get; set; } = RoutingKey.Empty;

    /// <summary>
    /// The AWS Role Name/ARN
    /// </summary>
    public string Role { get; set; } = role;

    /// <summary>
    /// Allow Brighter to give a priority to <see cref="MessageHeader.Topic"/> as destiny topic, in case it exists.
    /// </summary>
    public bool UseMessageTopicAsTarget { get; set; } = true;

    /// <summary>
    /// Action to be performed when a conflict happen during scheduler creating
    /// </summary>
    public OnSchedulerConflict OnConflict { get; set; }

    /// <summary>
    /// Action to be performed when checking role 
    /// </summary>
    public OnMissingRole MakeRole { get; set; }

    public IAmAMessageScheduler Create(IAmACommandProcessor processor)
    {
        var factory = new AWSClientFactory(connection);
        var roleArn = GetOrCreateRoleArnAsync(factory);
        if (!roleArn.IsCompleted)
        {
            BrighterAsyncContext.Run(async () => await roleArn);
        }

        var topicArn = GetTopicArnAsync(factory);
        if (!topicArn.IsCompleted)
        {
            BrighterAsyncContext.Run(async () => await topicArn);
        }

        var queueUrl = GetQueueUrlAsync(factory);
        if (!queueUrl.IsCompleted)
        {
            BrighterAsyncContext.Run(async () => await queueUrl);
        }

        return new AwsMessageScheduler(new AWSClientFactory(connection), GetOrCreateSchedulerId,
            new Scheduler
            {
                RoleArn = roleArn.Result,
                TopicArn = topicArn.Result,
                QueueUrl = queueUrl.Result,
                Topic = SchedulerTopicOrQueue,
                OnConflict = OnConflict,
                UseMessageTopicAsTarget = UseMessageTopicAsTarget,
                FlexibleTimeWindowMinutes = FlexibleTimeWindowMinutes
            },
            Group);
    }

    private ValueTask<string> GetTopicArnAsync(AWSClientFactory factory)
    {
        if (_topicArn != null)
        {
            return new ValueTask<string>(_topicArn);
        }

        if (Arn.IsArn(SchedulerTopicOrQueue))
        {
            _topicArn = SchedulerTopicOrQueue;
            return new ValueTask<string>(_topicArn);
        }

        return new ValueTask<string>(GetFromSnsTopicArnAsync());

        async Task<string> GetFromSnsTopicArnAsync()
        {
            using var client = factory.CreateSnsClient();
            var topic = await client.FindTopicAsync(SchedulerTopicOrQueue);
            _topicArn = topic?.TopicArn ?? "";
            return _topicArn;
        }
    }

    private ValueTask<string> GetQueueUrlAsync(AWSClientFactory factory)
    {
        {
            if (_queueUrl != null)
            {
                return new ValueTask<string>(_queueUrl);
            }

            if (Uri.TryCreate(SchedulerTopicOrQueue, UriKind.Absolute, out _))
            {
                _queueUrl = SchedulerTopicOrQueue;
                return new ValueTask<string>(_queueUrl);
            }

            return new ValueTask<string>(GetFromSqsQueueUrlAsync());

            async Task<string> GetFromSqsQueueUrlAsync()
            {
                using var client = factory.CreateSqsClient();
                try
                {
                    var queue = await client.GetQueueUrlAsync(SchedulerTopicOrQueue);
                    _queueUrl = queue.QueueUrl;
                }
                catch (QueueDoesNotExistException)
                {
                    _queueUrl = "";
                }

                return _queueUrl;
            }
        }
    }

    private ValueTask<string> GetOrCreateRoleArnAsync(AWSClientFactory factory)
    {
        if (_roleArn != null)
        {
            return new ValueTask<string>(_roleArn);
        }

        if (Arn.IsArn(Role))
        {
            return new ValueTask<string>(Role);
        }

        return new ValueTask<string>(GetOrCreateRoleArnFromIdentityAsync());

        async Task<string> GetOrCreateRoleArnFromIdentityAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                using var client = factory.CreateIdentityClient();
                var role = await client.GetRoleAsync(new GetRoleRequest { RoleName = Role });

                if (role.HttpStatusCode == HttpStatusCode.OK)
                {
                    _roleArn = role.Role.Arn;
                    return _roleArn;
                }

                if (MakeRole == OnMissingRole.AssumeRole)
                {
                    throw new InvalidOperationException($"Role '{Role}' not found");
                }

                return await CreateRoleArnAsync();
            }
            catch (NoSuchEntityException)
            {
                if (MakeRole == OnMissingRole.AssumeRole)
                {
                    throw new InvalidOperationException($"Role '{Role}' not found");
                }

                return await CreateRoleArnAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }

        async Task<string> CreateRoleArnAsync()
        {
            using var client = factory.CreateIdentityClient();
            var role = await client.CreateRoleAsync(new CreateRoleRequest
            {
                RoleName = Role,
                AssumeRolePolicyDocument = """
                                           {
                                                "Version": "2012-10-17",
                                                "Statement": [
                                                    {
                                                        "Effect": "Allow",
                                                        "Principal": {
                                                            "Service": "scheduler.amazonaws.com"
                                                        },
                                                        "Action": "sts:AssumeRole"
                                                        }
                                                ]
                                           }
                                           """
            });

            var policy = await client.CreatePolicyAsync(new CreatePolicyRequest
            {
                PolicyDocument = """
                                 {
                                    "Version": "2012-10-17",
                                    "Statement": [
                                    {
                                        "Effect": "Allow",
                                        "Action": [
                                            "sqs:SendMessage",
                                            "sns:Publish"
                                        ],
                                        "Resource": ["*"]
                                    }]
                                 }
                                 """,
            });

            await client.AttachRolePolicyAsync(new AttachRolePolicyRequest
            {
                RoleName = Role, PolicyArn = policy.Policy.Arn
            });

            _roleArn = role.Role.Arn;
            return _roleArn;
        }
    }
}
