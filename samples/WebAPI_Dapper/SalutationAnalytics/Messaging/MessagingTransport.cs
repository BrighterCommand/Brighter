namespace SalutationAnalytics.Messaging;

public static class MessagingGlobals
{
    //environment string key
    public const string BRIGHTER_TRANSPORT = "BRIGHTER_TRANSPORT";
    
    public const string RMQ = "RabbitMQ";
    public const string KAFKA = "Kafka";
}


/// <summary>
/// Which messaging transport are you using?
/// </summary>
public enum MessagingTransport
{
    Rmq,
    Kafka
}

