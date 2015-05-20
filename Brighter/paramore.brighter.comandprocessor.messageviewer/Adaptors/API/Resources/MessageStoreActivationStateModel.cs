using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources
{
    public class MessageStoreActivationStateModel
    {
        public MessageStoreActivationStateModel(){}
        public MessageStoreActivationStateModel(MessageStoreActivationState store) : this()
        {
            StoreType = store.GetType().Name;
            TypeName = store.TypeName;
            Name = store.Name;
            ConnectionString = store.ConnectionString;
            TableName = store.TableName;
        }
        
        public string ConnectionString { get; set; }
        public string Name { get; private set; }
        public string TypeName { get; private set; }
        public string StoreType { get; private set; }
        public string TableName { get; set; }
    }
}