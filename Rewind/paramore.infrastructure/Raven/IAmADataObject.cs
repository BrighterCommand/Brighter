namespace Paramore.Infrastructure.Raven
{
    public interface IAmADataObject
    {
        //RavenDB needs properties to store documents and public properties for LINQ queries
        //it can also be useful to check state
        //so we can ask an aggregate for its IDataObject, which we then store from Raven
    }
}
