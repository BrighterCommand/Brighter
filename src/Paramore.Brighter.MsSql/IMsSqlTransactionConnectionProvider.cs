using Microsoft.Data.SqlClient;

namespace Paramore.Brighter.MsSql
{
    public interface IMsSqlTransactionConnectionProvider : IMsSqlConnectionProvider, IAmABoxTransactionConnectionProvider
    {
    }
}
