namespace Paramore.Brighter
{
    public interface IAmAMessageTransform
    {
        Message Wrap(Message message);
        Message Unwrap(Message message);
    }
}
