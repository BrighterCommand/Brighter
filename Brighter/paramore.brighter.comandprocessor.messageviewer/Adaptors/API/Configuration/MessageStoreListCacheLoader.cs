using paramore.brighter.commandprocessor.Logging;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messagestore.ravendb;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using Raven.Client.Document;
using Raven.Client.Embedded;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration
{

    public interface IMessageStoreListCacheLoader
    {
        IMessageStoreActivationState Load();
    }

    public class MessageStoreListCacheLoader : IMessageStoreListCacheLoader
    {
        private ILog _logger = LogProvider.GetLogger("MessageStoreListCacheLoader");
        private readonly IMessageStoreActivationState _messageStoreActivationState;

        public MessageStoreListCacheLoader(IMessageStoreActivationState messageStoreActivationState)
        {
            _messageStoreActivationState= messageStoreActivationState;
        }

        public IMessageStoreActivationState Load()
        {
            _messageStoreActivationState.Set(MessageStoreType.SqlServer,
                (storeConfig) => new MsSqlMessageStore(
                    new MsSqlMessageStoreConfiguration(storeConfig.ConnectionString, storeConfig.TableName,
                        MsSqlMessageStoreConfiguration.DatabaseType.MsSqlServer), _logger));

            _messageStoreActivationState.Set(MessageStoreType.SqlCe,
                (storeConfig) => new MsSqlMessageStore(
                    new MsSqlMessageStoreConfiguration(storeConfig.ConnectionString, storeConfig.TableName,
                        MsSqlMessageStoreConfiguration.DatabaseType.SqlCe), _logger));
            _messageStoreActivationState.Set( MessageStoreType.RavenRemote,
                (storeConfig) =>
                {
                    var documentStore = new DocumentStore();
                    documentStore.ParseConnectionString(storeConfig.ConnectionString);
                    return new RavenMessageStore(documentStore.Initialize(), _logger);
                });
            _messageStoreActivationState.Set(MessageStoreType.RavenLocal,
                (storeConfig) =>
                {
                    //var embeddableDocumentStore = new EmbeddableDocumentStore {UseEmbeddedHttpServer = true};
                    var embeddableDocumentStore = new EmbeddableDocumentStore ();
                    embeddableDocumentStore.DataDirectory =
                        storeConfig.ConnectionString.Replace(" ", "").Replace("Url=", "").Replace("DataDir=", "");
                    return new RavenMessageStore(embeddableDocumentStore.Initialize(), _logger);
                });
            return _messageStoreActivationState;
        }
    }
}