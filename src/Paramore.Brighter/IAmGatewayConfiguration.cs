namespace Paramore.Brighter
{
    /// <summary>
    /// A marker interface. Transports will need configuration that lets them talk to the message-oriented-middleware they use. That is common to
    /// both a consumer and a producer. Use this interface to indicate the class that has that role
    /// Use <see cref="Subscription "/>for consumer specific logic, specializing it for your platform
    /// Use <see cref="Publication"/> for producer specific logic, specializing it for your platform.
    /// It is common for us to have a base class on a transport that abstracts connecting to the message-oriented middleware and derive a produce and consumer
    /// from that. The properties you add to the derived class should normally be used within that base gateway and are often private to the base class
    /// not shared with the derived classes as they are specific to how we connect not how we produce or consume
    /// </summary>
    public interface IAmGatewayConfiguration;
}
