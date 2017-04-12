using System;
using GenericListener.Adapters.Services;
using Topshelf;
using Topshelf.Hosts;

namespace GenericListener.Adapters.ServiceHost
{
    /// <summary>
    /// GenericListener is an example of using Brighter with C# generics to reduce the boilerplate
    /// code required to listen to messages (usually events) in a system. One usage of this might be to
    /// capture all events in a system and feed into a backing store such as EventStore to then perform
    /// Complex Event Processing (CEP).
    /// 
    /// Typically for each message a Brighter solution will require;
    ///     DataType POCO
    ///     DataType Handler
    ///     DataType MessageMapper
    /// 
    /// If you are aiming to listen to (for example) 60 events in your system this effectively means
    /// 180 C# class files, and potentially each with custom mapping code. 
    /// 
    /// Taking the notion that any backing store - such as EventStore - will store in a schemaless JSON 
    /// it is redundant to deserialize a messsage off a qeueue into a POCO, only to serialize back into 
    /// JSON. As such it is possible to imagine you can have a generic mapper and/or handler that can 
    /// deal with the majority of event objects.
    /// 
    /// In the instance where a custom version of a mapper and/or handler is required for a particular 
    /// object type, or type for which it's derived from, this solution somewhat crudely aims to hoist 
    /// those accordingly.
    /// 
    /// public class MyEvent : Event(StoredEvent) {}
    /// public class MySuperEvent : MyEvent {}
    /// public class MyDuperEvent : MyEvent {}
    /// 
    /// For example;
    /// 
    /// RegisterGenericHandlersFor<MyEvent>(config);
    /// 
    /// The above will look for all derived types of MyEvent and select handlers and/or mappers based on;
    /// 
    /// 1. Match Type implementing IAmMessageMapper<MySuperEvent>
    /// 2. Create Type of MyEventMapper<T> implementing IAmMessageMapper<T> where T is MySuperEvent
    /// 3. Create Type of GenericMapper<T> implementing IamMessageMapper<T> where T is MySuperEvent
    /// 4. .. do the same for IHandlerRequests<>.
    /// 
    /// Thus to listen to an additional event;
    /// 
    /// 1. Add connection to <serviceActivatorConnections /> with DataType (e.g. MySuperEvent)
    /// 2. Add class in an inheritance chain that suits (e.g. public class MySuperEvent : MyEvent {})
    /// 3. Optionally add MyEventMapper<T> (if not exists) or MySuperEventMapper : IAmMessageMapper<MySuperEvent> {}
    /// 4. Optionally add MyEventHandler<T> (if not exists) or MySuperEventHandler : IHandleRequests<MySuperEvent> {}
    /// </summary>
    internal class Program
    {
        public static void Main()
        {
            HostFactory.Run(x => x.Service<GenericListenerService>(sc =>
            {
                x.UseLog4Net();

                sc.ConstructUsing(() => new GenericListenerService());

                // the start and stop methods for the service
                sc.WhenStarted((s, hostcontrol) =>
                {
                    if (hostcontrol is ConsoleRunHost)
                    {
                        Console.WriteLine("This example program assumes that you have an EventStore instance\r\nrunning on 127.0.0.1:7113 (TCP) and 127.0.0.1:7114 (HTTP).\r\n");
                        Console.WriteLine("Press any key to continue..");
                        Console.ReadLine();
                    }

                    return s.Start(hostcontrol);
                });

                sc.WhenStopped((s, hostcontrol) => s.Stop(hostcontrol));

                // optional, when shutdown is supported
                sc.WhenShutdown((s, hostcontrol) => s.Shutdown(hostcontrol));
            }));
        }
    }
}
