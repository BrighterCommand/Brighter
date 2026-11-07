using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.MySQL.Tests.TestDoubles;

public class RecordingConnectionProvider(IAmARelationalDbConnectionProvider inner) : IAmARelationalDbConnectionProvider
{
    private readonly List<RecordedCommand> _executed = [];

    public IReadOnlyList<RecordedCommand> ExecutedCommands => _executed;

    public DbConnection GetConnection() => new RecordingDbConnection(inner.GetConnection(), _executed);

    public async Task<DbConnection> GetConnectionAsync(CancellationToken cancellationToken) =>
        new RecordingDbConnection(await inner.GetConnectionAsync(cancellationToken), _executed);
}
