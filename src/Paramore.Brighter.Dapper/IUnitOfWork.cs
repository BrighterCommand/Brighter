using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Dapper
{
    public interface IUnitOfWork : IDisposable
    {
        DbTransaction BeginOrGetTransaction();
        Task<DbTransaction> BeginOrGetTransactionAsync(CancellationToken cancellationToken);
        void Commit();
        DbConnection Database { get; }
        bool HasTransaction();
    }
}
