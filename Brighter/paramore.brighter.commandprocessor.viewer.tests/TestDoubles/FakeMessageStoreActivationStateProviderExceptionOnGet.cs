using System;
using System.Collections.Generic;
using paramore.brighter.commandprocessor.messageviewer.Ports.Domain;

namespace paramore.brighter.commandprocessor.viewer.tests.TestDoubles
{
    public class FakeMessageStoreActivationStateProviderExceptionOnGet : FakeMessageStoreActivationStateProvider
    {
        public FakeMessageStoreActivationStateProviderExceptionOnGet() : base()
        {
        }

        public override IEnumerable<MessageStoreActivationState> Get()
        {
            throw new Exception("Fake exception on GET");
        }
    }
}