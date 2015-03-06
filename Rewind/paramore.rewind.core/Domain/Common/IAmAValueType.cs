namespace Paramore.Rewind.Core.Domain.Common
{
    public interface IAmAValueType<out T>
    {
        T Value { get; }
    }
}