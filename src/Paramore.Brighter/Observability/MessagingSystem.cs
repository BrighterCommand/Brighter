namespace Paramore.Brighter.Observability;

/// <summary>
/// The messaging system used to send a message
/// </summary>
public enum MessagingSystem
{
    activemq = 0,
    aws_sqs,
    eventgrid,
    eventhubs,
    servicebus,
    gcp_pubsub,
    jms,
    kafka,
    rabbitmq,
    rocketmq
}

