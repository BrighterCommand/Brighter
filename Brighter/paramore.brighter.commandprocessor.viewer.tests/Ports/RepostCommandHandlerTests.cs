using System.Collections.Generic;
using Machine.Specifications;
using paramore.brighter.commandprocessor.messagestore.mssql;
using paramore.brighter.commandprocessor.messageviewer.Adaptors.API.Resources;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;
using paramore.brighter.commandprocessor.messageviewer.Ports.Handlers;
using paramore.brighter.commandprocessor.messageviewer.Ports.ViewModelRetrievers;
using paramore.brighter.commandprocessor.viewer.tests.TestDoubles;

namespace paramore.brighter.commandprocessor.viewer.tests.Ports
{
    [Subject(typeof (RepostCommandHandler))]
    public class RepostCommandHandlerTests
    {
        public class When_retrieving_json_for_valid_item
        {
            private Establish _context = () =>
            {
                _messageStore = MessageStoreActivationStateFactory.Create(_storeName,
                    typeof (MsSqlMessageStore).FullName, _storeName, "table2");
                var fakeStoreListProvider = new FakeMessageStoreActivationStateProvider(_messageStore);

                var fakeStore = new FakeMessageStoreWithViewer();

                var fakeMessageStoreFactory = new FakeMessageStoreViewerFactory(fakeStore, _storeName);


                //command = new RepostCommand{MessageIds = };
                repostHandler = new RepostCommandHandler();
            };

            private Because _of_GET = () => repostHandler.Handle(command);

            private It should_return_model = () =>
            {
                var model = _result.Result;
                model.ShouldNotBeNull();
                model.Name.ShouldEqual(_messageStore.Name);
                model.ConnectionString.ShouldEqual(_messageStore.ConnectionString);
                model.TableName.ShouldEqual(_messageStore.TableName);
                model.TypeName.ShouldEqual(_messageStore.TypeName);
                model.Name.ShouldEqual(_messageStore.Name);
            };

            private It shoudl_Error = () => true.ShouldBeFalse();

            private static List<MessageStoreActivationState> _messageStores;
            private static MessageStoreViewerModelRetriever _messageStoreViewerModelRetriever;
            private static List<MessageStoreActivationState> _ravenMessageStores;
            private static ViewModelRetrieverResult<MessageStoreViewerModel, MessageStoreViewerModelError> _result;
            private static string _storeName = "storeItemtestStoreName";
            private static MessageStoreActivationState _messageStore;
            private static RepostCommandHandler repostHandler;
            private static RepostCommand command;
        }
    }
}