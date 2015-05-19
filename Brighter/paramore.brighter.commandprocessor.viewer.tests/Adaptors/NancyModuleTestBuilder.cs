using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Nancy.Testing;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Configuration;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Handlers;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;
using paramore.commandprocessor.tests.CommandProcessors.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Adaptors
{
    public static class NancyModuleTestBuilder
    {
        public static void MessagesModule(
            this ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator config,
            IMessageListViewModelRetriever messageListViewModelRetriever)
        {
            config.Module<MessagesNancyModule>();
            config.Dependencies<IMessageListViewModelRetriever>(messageListViewModelRetriever);
        }
        public static void StoresModule(
            this ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator config,
            IMessageStoreActivationStateListViewModelRetriever storeListRetriever,
            IMessageStoreViewerModelRetriever storeRetriever,
            IMessageListViewModelRetriever messageListRetriver)
        {
            config.Module<StoresNancyModule>();
            config.Dependencies<IMessageStoreActivationStateListViewModelRetriever>(storeListRetriever);
            config.Dependencies<IMessageStoreViewerModelRetriever>(storeRetriever);
            config.Dependencies<IMessageListViewModelRetriever>(messageListRetriver);
        }

        public static void StoresModule(this ConfigurableBootstrapper.ConfigurableBootstrapperConfigurator config, 
                IEnumerable<MessageStoreActivationState> stores)
        {
            var listViewRetriever = new FakeActivationListModelRetriever(new MessageStoreActivationStateListModel(stores));
            var storeRetriever = new FakeMessageStoreViewerModelRetriever(new MessageStoreViewerModel(new FakeMessageStore(), stores.FirstOrDefault()));
            var messageRetriever = new FakeMessageListViewModelRetriever(new MessageListModel(new List<Message>()));

            config.StoresModule(listViewRetriever, storeRetriever, messageRetriever);
        }
    }
}