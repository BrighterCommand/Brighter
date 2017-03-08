using System.Collections.Generic;

namespace Paramore.Brighter.MessageViewer.WindowsService.Configuration
{
    public class MessageViewerSectionAdapater
    {
        public static MessageViewerConfiguration Map(MessageViewerSection section)
        {
            var config = new MessageViewerConfiguration();
            config.Port = section.Port;
            config.Stores = new List<MessageViewerConfigurationStore>(section.Stores.Count);
            foreach (MessageViewerStoresElement store in section.Stores)
            {
                var messageViewerStoresElement = new MessageViewerConfigurationStore();
                messageViewerStoresElement.ConnectionString = store.ConnectionString;
                messageViewerStoresElement.Name = store.Name;
                messageViewerStoresElement.TableName = store.TableName; //opt
                messageViewerStoresElement.Type = store.Type;

                config.Stores.Add(messageViewerStoresElement);
            }

            config.Producer = new MessageViewerConfigurationProducer();
            config.Producer.AssemblyQualifiedName = config.Producer.AssemblyQualifiedName;

            return config;
        }
    }
}