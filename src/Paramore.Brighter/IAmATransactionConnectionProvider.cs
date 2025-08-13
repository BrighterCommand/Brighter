using System.Data.Common;

namespace Paramore.Brighter
{
    public interface IAmATransactionConnectionProvider : IAmARelationalDbConnectionProvider, IAmABoxTransactionProvider<DbTransaction>;
}
