using Nancy.TinyIoc;
using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration
{
    public class DependencyResolver
    {
        internal static void ConfigureDependencies(TinyIoCContainer container)
        {
            var logger = LogProvider.GetLogger("MainNancyModule");
            logger.Log(LogLevel.Debug, () => "GET on messages");

            var messageStore =
                new MsSqlMessageStore(
                    new MsSqlMessageStoreConfiguration("Server=.;Database=brighterMessageStore;Trusted_Connection=True",
                        "messages", MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer), logger);

            container.Register(messageStore);
            container.Register(typeof (IMessageStoreActivationStateProvider), typeof (MessageStoreActivationStateProvider));
            container.Register(typeof (IMessageStoreViewerFactory), typeof (MessageStoreViewerFactory));
        }
    }
}
