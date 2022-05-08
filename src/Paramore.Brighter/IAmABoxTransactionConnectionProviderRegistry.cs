namespace Paramore.Brighter
{
    public interface IAmABoxTransactionConnectionProviderRegistry
    {
        IAmABoxTransactionConnectionProvider GetDefault();
        IAmABoxTransactionConnectionProvider Lookup(string name);
    }
}
