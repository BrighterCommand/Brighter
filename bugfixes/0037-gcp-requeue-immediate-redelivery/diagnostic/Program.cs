// FR-15 requeue-zero-delay diagnostic for https://github.com/BrighterCommand/Brighter/issues/4321
//
// This calls Brighter's own GcpPullMessageConsumer.Receive/Requeue directly against REAL Pub/Sub
// (not the emulator) to separate the two live hypotheses in bugfix.md:
//
//   H1: short client-side Pull deadlines (the test's 500ms poll window) lose an in-flight
//       redelivered message, which then sits leased until the ack deadline expires.
//   H2: real Pub/Sub simply doesn't act on a unary-Pull ModifyAckDeadline(0) promptly, so
//       redelivery always falls back to the ORIGINAL lease, regardless of poll window.
//
// Usage:
//   export GOOGLE_CLOUD_PROJECT=your-project-id   (see ../gcp-setup.md)
//   dotnet run -- [ackDeadlineSeconds] [pollTimeoutMs] [maxPolls]
//
// Suggested runs (see bugfix.md "What best separates the leads"):
//   dotnet run -- 10 500   60   # reproduces the failing shape (ackDeadline=10s, 500ms polls)
//   dotnet run -- 10 15000 4    # long polls, same ack deadline — redelivery in ~1s => H1
//                                # redelivery ~10s after the INITIAL receive regardless => H2
//   dotnet run -- 60 15000 4    # long ack deadline too — under H2 redelivery tracks ~60s;
//                                # under H1 it still tracks modack+~0 (no cancelled poll to lose it)

using System.Diagnostics;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Paramore.Brighter;
using Paramore.Brighter.MessagingGateway.GcpPubSub;
using GcpSubscription = Google.Cloud.PubSub.V1.Subscription;
using GcpSubscriptionName = Google.Cloud.PubSub.V1.SubscriptionName;

var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
if (string.IsNullOrWhiteSpace(projectId))
{
    Console.Error.WriteLine("Set GOOGLE_CLOUD_PROJECT to your GCP project id first (see ../gcp-setup.md).");
    return 1;
}

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST")))
{
    Console.Error.WriteLine("PUBSUB_EMULATOR_HOST is set — unset it. This diagnostic must hit real Pub/Sub,");
    Console.Error.WriteLine("not the emulator, or it will just reproduce the (uninformative) emulator pass.");
    return 1;
}

var ackDeadlineSeconds = args.Length > 0 ? int.Parse(args[0]) : 10;
var pollTimeoutMs = args.Length > 1 ? int.Parse(args[1]) : 500;
var maxPolls = args.Length > 2 ? int.Parse(args[2]) : 60;

var topicName = TopicName.FromProjectTopic(projectId, $"fr15-diag-{Guid.NewGuid():N}");
var subscriptionName = GcpSubscriptionName.FromProjectSubscription(projectId, $"fr15-diag-{Guid.NewGuid():N}");

Console.WriteLine($"Project:      {projectId}");
Console.WriteLine($"Topic:        {topicName}");
Console.WriteLine($"Subscription: {subscriptionName}  (ackDeadlineSeconds={ackDeadlineSeconds})");
Console.WriteLine($"Poll window:  {pollTimeoutMs} ms per poll, up to {maxPolls} polls after Requeue");
Console.WriteLine();

var credential = GoogleCredential.GetApplicationDefault();
var publisherApi = new PublisherServiceApiClientBuilder { Credential = credential }.Build();
var subscriberApi = new SubscriberServiceApiClientBuilder { Credential = credential }.Build();

try
{
    Console.WriteLine("Creating topic and subscription on real Pub/Sub...");
    publisherApi.CreateTopic(new Topic { TopicName = topicName });
    subscriberApi.CreateSubscription(new GcpSubscription
    {
        SubscriptionName = subscriptionName,
        TopicAsTopicName = topicName,
        AckDeadlineSeconds = ackDeadlineSeconds,
    });

    // Same classes production code uses (GcpMessagingGatewayConnection / GcpPullMessageConsumer),
    // deliberately with no EmulatorDetection wiring, so this always talks to real GCP regardless
    // of any leftover PUBSUB_EMULATOR_HOST from other test runs.
    var connection = new GcpMessagingGatewayConnection { Credential = credential, ProjectId = projectId };
    var consumer = new GcpPullMessageConsumer(connection, subscriptionName, batchSize: 10, TimeProvider.System);

    var publisherClient = await new PublisherClientBuilder { Credential = credential, TopicName = topicName }.BuildAsync();
    var messageId = await publisherClient.PublishAsync(
        new PubsubMessage { Data = ByteString.CopyFromUtf8("fr15-diagnostic-payload") });
    Console.WriteLine($"Published message id={messageId} at {DateTimeOffset.UtcNow:O}");

    // Initial receive — mirrors the FR-15 test's Receive(5000ms).
    var received = new Message();
    var receiveDeadline = Stopwatch.StartNew();
    while (receiveDeadline.Elapsed < TimeSpan.FromSeconds(30))
    {
        var batch = consumer.Receive(TimeSpan.FromSeconds(5));
        if (batch.Length > 0 && batch[0].Header.MessageType != MessageType.MT_NONE)
        {
            received = batch[0];
            break;
        }
    }

    if (received.Header.MessageType == MessageType.MT_NONE)
    {
        Console.Error.WriteLine("Never received the published message within 30s — aborting.");
        return 1;
    }

    var initialReceiveAt = DateTimeOffset.UtcNow;
    Console.WriteLine($"Initial receive at {initialReceiveAt:O} (message id={received.Id})");

    // The call under test.
    var requeued = consumer.Requeue(received, TimeSpan.Zero);
    var requeueReturnedAt = DateTimeOffset.UtcNow;
    Console.WriteLine($"Requeue(msg, TimeSpan.Zero) returned {requeued} at {requeueReturnedAt:O}");

    if (!requeued)
    {
        Console.Error.WriteLine("Requeue returned false — cannot measure redelivery timing.");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("poll  start           duration(ms)  result   sinceRequeue(s)  sinceInitialReceive(s)");

    var sinceRequeue = Stopwatch.StartNew();
    var redeliveredAtPoll = -1;

    for (var i = 1; i <= maxPolls; i++)
    {
        var pollStart = DateTimeOffset.UtcNow;
        var pollWatch = Stopwatch.StartNew();
        var polled = consumer.Receive(TimeSpan.FromMilliseconds(pollTimeoutMs));
        pollWatch.Stop();

        var gotMessage = polled.Length > 0 && polled[0].Header.MessageType != MessageType.MT_NONE;

        Console.WriteLine(
            $"{i,4}  {pollStart:HH:mm:ss.fff}  {pollWatch.Elapsed.TotalMilliseconds,10:F0}  " +
            $"{(gotMessage ? "MESSAGE" : "empty  ")}  {sinceRequeue.Elapsed.TotalSeconds,13:F2}  " +
            $"{(pollStart - initialReceiveAt).TotalSeconds,20:F2}");

        if (gotMessage)
        {
            redeliveredAtPoll = i;
            consumer.Acknowledge(polled[0]);
            break;
        }
    }

    Console.WriteLine();
    if (redeliveredAtPoll < 0)
    {
        Console.WriteLine($"NOT redelivered within {maxPolls} polls (~{maxPolls * pollTimeoutMs / 1000.0:F1}s of polling).");
    }
    else
    {
        Console.WriteLine(
            $"Redelivered on poll {redeliveredAtPoll}: {sinceRequeue.Elapsed.TotalSeconds:F2}s after Requeue returned, " +
            $"{(DateTimeOffset.UtcNow - initialReceiveAt).TotalSeconds:F2}s after the initial receive.");
        Console.WriteLine();
        Console.WriteLine("Reading the result (see bugfix.md):");
        Console.WriteLine("  ~0-2s since Requeue, any poll window   -> modack works; H1 (lost-in-flight polls) is the whole story.");
        Console.WriteLine("  ~ackDeadlineSeconds since INITIAL receive, regardless of poll window -> H2 (platform limit on unary Pull).");
    }
}
finally
{
    Console.WriteLine();
    Console.WriteLine("Cleaning up topic/subscription...");
    try
    {
        subscriberApi.DeleteSubscription(new DeleteSubscriptionRequest { SubscriptionAsSubscriptionName = subscriptionName });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  subscription cleanup failed (may already be gone): {ex.Message}");
    }

    try
    {
        publisherApi.DeleteTopic(new DeleteTopicRequest { TopicAsTopicName = topicName });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"  topic cleanup failed (may already be gone): {ex.Message}");
    }
}

return 0;
