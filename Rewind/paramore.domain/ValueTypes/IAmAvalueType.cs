namespace Paramore.Domain.ValueTypes
{
    public interface IAmAValueType<out T>
    {
        T Value { get; }
    }
}