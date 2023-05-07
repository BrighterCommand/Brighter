using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    public abstract class RelationalDbConnectionProvider : IAmARelationalDbConnectionProvider
    {
        public abstract DbConnection GetConnection();

        public virtual async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<DbConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
            tcs.SetResult(GetConnection());
            return await tcs.Task;
        }

        public virtual DbTransaction GetTransaction()
        {
            //This connection factory does not support transactions
            return null;
        }

        public virtual bool HasOpenTransaction { get => false; }

        public virtual bool IsSharedConnection { get => false; }
    }
}
