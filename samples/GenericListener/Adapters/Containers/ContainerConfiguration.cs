using System;
using System.Net;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using GenericListener.Adapters.EventStore;
using GenericListener.Ports.Indexers;
using TinyIoc;

namespace GenericListener.Adapters.Containers
{
    public class TinyIoCContainerConfiguration
    {
        public TinyIoCContainer Build()
        {
            var container = new TinyIoCContainer();

            container.Register<IEventStoreConnection>((c, po) =>
            {
                var settings = ConnectionSettings
                    .Create()
                    .KeepReconnecting()
                    .EnableVerboseLogging()
                    .SetDefaultUserCredentials(new UserCredentials("admin", "changeit"))
                    .UseCustomLogger(new EventStoreLogger())
                    .WithConnectionTimeoutOf(TimeSpan.FromSeconds(30))
                    .Build();

                var connection = EventStoreConnection.Create(settings, new IPEndPoint(IPAddress.Loopback, 7113));
                connection.ConnectAsync().Wait();

#if DEBUG
                // If we are running on a DEBUG build then let's make our life easy
                // by ensuring EventStore has default ACLs.

                const string settingsJson = @"{
    ""$userStreamAcl"" : {
        ""$r""  : ""$all"",
        ""$w""  : ""$all"",
        ""$d""  : ""$all"",
        ""$mr"" : ""$all"",
        ""$mw"" : ""$all""
    },
    ""$systemStreamAcl"" : {
        ""$r""  : ""$admins"",
        ""$w""  : ""$admins"",
        ""$d""  : ""$admins"",
        ""$mr"" : ""$admins"",
        ""$mw"" : ""$admins""
    }
}";
                try
                {
                    connection.AppendToStreamAsync(
                        "$settings",
                        ExpectedVersion.EmptyStream,
                        new EventData(Guid.NewGuid(), "settings", true, Encoding.UTF8.GetBytes(settingsJson), null))
                        .Wait();
                }
                catch (AggregateException e)
                {
                    if (e.InnerExceptions.Count != 1 || !(e.InnerException is WrongExpectedVersionException))
                    {
                        throw;
                    }
                }
#endif
                return connection;
            });

            //// TinyIoC just can't handle the generic resolution of this, but Castle/StructueMap certainly can

            //container.Register(typeof(IEventStoreWriter<>), typeof(EventStoreWriter<>)).AsSingleton();
            //container.Register<ITaskReminderSentEventIndexer, TaskReminderSentEventIndexer>().AsMultiInstance();
            //container.Register(typeof(IGenericFeedEventIndexer<>), typeof(GenericEventIndexer<>)).AsMultiInstance();

            container.Register(typeof(IEventStoreWriter<>), typeof(EventStoreWriter<>)).AsMultiInstance(); //Should be singleton
            container.Register(typeof(IGenericFeedEventIndexer<>), typeof (GenericEventIndexer<>)).AsMultiInstance(); //should be transient

            return container;
        }
    }
}