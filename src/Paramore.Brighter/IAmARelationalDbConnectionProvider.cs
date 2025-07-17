using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter
{
    /// <summary>
    /// Providers an abstraction over a relational database connection; implementations may allow connection and
    /// transaction sharing by holding state, thus a Close method is provided.
    /// </summary>
    public interface IAmARelationalDbConnectionProvider : IAmAConnectionProvider
    {
        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// </summary>
        /// <returns>A database connection</returns>
        DbConnection GetConnection();
        
        /// <summary>
        /// Gets a existing Connection; creates a new one if it does not exist
        /// The connection is not opened, you need to open it yourself.
        /// </summary>
        /// <returns>A database connection</returns>
        Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken);
    }
}
