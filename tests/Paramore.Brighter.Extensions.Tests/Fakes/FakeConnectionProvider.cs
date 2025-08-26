using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Extensions.Tests.Fakes
{
    public class FakeConnectionProvider : RelationalDbConnectionProvider
    {
        public override DbConnection GetConnection()
        {
            throw new NotImplementedException();
        }

        public override async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
