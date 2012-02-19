using Nancy;

namespace paramore.commandprocessor
{
    public interface IRequestContext
    {
        dynamic Bag { get; }
    }
}