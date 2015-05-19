using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    internal class FakeMessageStoreViewerFactory : IMessageStoreViewerFactory
    {
        private readonly IAmAMessageStore<Message> _fakeStore;
        private string storeName;

        public FakeMessageStoreViewerFactory(IAmAMessageStore<Message> fakeStore, string storeName)
        {
            _fakeStore = fakeStore;
            this.storeName = storeName;
        }
        private FakeMessageStoreViewerFactory(){}

        public static FakeMessageStoreViewerFactory CreateEmptyFactory()
        {
            return new FakeMessageStoreViewerFactory();
        }

        public IAmAMessageStore<Message> Connect(string messageStoreName)
        {
            return (messageStoreName == storeName)? _fakeStore:null;
        }
    }
}