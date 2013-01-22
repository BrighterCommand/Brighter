namespace Paramore.Domain.Common
{
    public interface IAmAValueType<out T>
    {
        T Value { get; }
    }
}