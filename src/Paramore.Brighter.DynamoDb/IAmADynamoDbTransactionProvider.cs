using System.Threading;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.Model;

namespace Paramore.Brighter.DynamoDb
{
    public interface IAmADynamoDbTransactionProvider : IAmADynamoDbConnectionProvider, IAmABoxTransactionProvider<TransactWriteItemsRequest>
    {
    }
}
