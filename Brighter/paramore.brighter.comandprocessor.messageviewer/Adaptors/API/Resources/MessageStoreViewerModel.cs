using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources
{
    public class MessageStoreViewerModel
    {
        public MessageStoreViewerModel(IAmAMessageStore<Message> connectedStore, MessageStoreActivationState foundState)
        {
            Name = foundState.Name;
            StoreType = foundState.StoreType;
            TypeName = foundState.TypeName;
            ConnectionString = foundState.ConnectionString;
            TableName = foundState.TableName;
            //TODO: ++ double something with connectedStore
        }

        public MessageStoreViewerModel()
        {
        }

        public MessageStoreType StoreType { get; private set; }
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string ConnectionString { get; set; }
        public string TableName { get; set; }
    }
}