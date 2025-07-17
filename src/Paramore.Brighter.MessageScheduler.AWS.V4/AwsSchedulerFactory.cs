using System.Net;
using Amazon;
using Amazon.IdentityManagement.Model;
using Paramore.Brighter.MessagingGateway.AWS.V4;
using Paramore.Brighter.Tasks;

namespace Paramore.Brighter.MessageScheduler.AWS.V4;

/// <summary>
/// The Aws message Scheduler factory
/// </summary>
public class AwsSchedulerFactory(AWSMessagingGatewayConnection connection, string role)
    : IAmAMessageSchedulerFactory, IAmARequestSchedulerFactory
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _roleArn;

    /// <summary>
    /// The <see cref="System.TimeProvider"/>
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    /// The AWS Scheduler group
    /// </summary>
    public SchedulerGroup Group { get; set; } = new();

    /// <summary>
    /// Get or create a message scheduler id
    /// </summary>
    public Func<Message, string> GetOrCreateMessageSchedulerId { get; set; } = _ => Uuid.NewAsString();

    /// <summary>
    /// Get or create a request scheduler id
    /// </summary>
    public Func<IRequest, string> GetOrCreateRequestSchedulerId { get; set; } = _ => Uuid.NewAsString();

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

    private AwsScheduler CreateAwsScheduler()
    {
        var factory = new AWSClientFactory(connection);
        if (string.IsNullOrEmpty(_roleArn))
        {
            _roleArn = BrighterAsyncContext.Run(async () => await GetOrCreateRoleArnAsync(factory, Role));
        }

        return new AwsScheduler(new AWSClientFactory(connection),
            TimeProvider,
            GetOrCreateMessageSchedulerId,
            GetOrCreateRequestSchedulerId,
            new Scheduler
            {
                RoleArn = _roleArn!,
                SchedulerTopic = SchedulerTopicOrQueue,
                OnConflict = OnConflict,
                UseMessageTopicAsTarget = UseMessageTopicAsTarget,
                FlexibleTimeWindowMinutes = FlexibleTimeWindowMinutes
            },
            Group);
    }

    private async Task<string> GetOrCreateRoleArnAsync(AWSClientFactory factory, string role)
    {
        if (MakeRole == OnMissingRole.Assume && Arn.IsArn(role))
        {
            return role;
        }

        await _semaphore.WaitAsync();
        try
        {
            using var client = factory.CreateIdentityClient();
            var awsRole = await client.GetRoleAsync(new GetRoleRequest { RoleName = role });

            if (awsRole.HttpStatusCode == HttpStatusCode.OK)
            {
                return awsRole.Role.Arn;
            }

            if (MakeRole != OnMissingRole.Create)
            {
                throw new InvalidOperationException($"Role '{role}' not found");
            }

            return await CreateRoleArnAsync();
        }
        catch (NoSuchEntityException)
        {
            if (MakeRole == OnMissingRole.Assume)
            {
                throw new InvalidOperationException($"Role '{Role}' not found");
            }

            return await CreateRoleArnAsync();
        }
        finally
        {
            _semaphore.Release();
        }

        async Task<string> CreateRoleArnAsync()
        {
            using var client = factory.CreateIdentityClient();
            var createdRole = await client.CreateRoleAsync(new CreateRoleRequest
            {
                RoleName = role,
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
                RoleName = role, PolicyArn = policy.Policy.Arn
            });

            return createdRole.Role.Arn;
        }
    }

    /// <inheritdoc />
    public IAmAMessageScheduler Create(IAmACommandProcessor processor) => CreateAwsScheduler();

    /// <inheritdoc />
    public IAmARequestSchedulerSync CreateSync(IAmACommandProcessor processor) => CreateAwsScheduler();

    /// <inheritdoc />
    public IAmARequestSchedulerAsync CreateAsync(IAmACommandProcessor processor) => CreateAwsScheduler();
}
