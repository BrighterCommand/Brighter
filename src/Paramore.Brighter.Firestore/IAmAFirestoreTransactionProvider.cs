namespace Paramore.Brighter.Firestore;

public interface IAmAFirestoreTransactionProvider : IAmAFirestoreConnectionProvider, IAmABoxTransactionProvider<FirestoreTransaction>
{
    
}
