using System;

namespace Paramore.Brighter
{
    public interface ISupportPublishConfirmation
    {
        event Action<bool, Guid> OnMessagePublished;
    }
}
