using Paramore.Brighter;
using Paramore.Brighter.BoxProvisioning;
using Paramore.Brighter.BoxProvisioning.MsSql;
using Paramore.Brighter.BoxProvisioning.MySql;
using Paramore.Brighter.BoxProvisioning.PostgreSql;
using Paramore.Brighter.BoxProvisioning.Sqlite;

namespace DbMaker;

public static class BoxProvisioningFactory
{
    public static void AddOutbox(
        BoxProvisioningOptions options,
        Rdbms rdbms,
        IAmARelationalDatabaseConfiguration configuration)
    {
        switch (rdbms)
        {
            case Rdbms.MsSql:
                options.AddMsSqlOutbox(configuration);
                break;
            case Rdbms.MySql:
                options.AddMySqlOutbox(configuration);
                break;
            case Rdbms.Postgres:
                options.AddPostgreSqlOutbox(configuration);
                break;
            case Rdbms.Sqlite:
                options.AddSqliteOutbox(configuration);
                break;
            default:
                throw new InvalidOperationException("Unknown Db type for box provisioning outbox");
        }
    }

    public static void AddInbox(
        BoxProvisioningOptions options,
        Rdbms rdbms,
        IAmARelationalDatabaseConfiguration configuration)
    {
        switch (rdbms)
        {
            case Rdbms.MsSql:
                options.AddMsSqlInbox(configuration);
                break;
            case Rdbms.MySql:
                options.AddMySqlInbox(configuration);
                break;
            case Rdbms.Postgres:
                options.AddPostgreSqlInbox(configuration);
                break;
            case Rdbms.Sqlite:
                options.AddSqliteInbox(configuration);
                break;
            default:
                throw new InvalidOperationException("Unknown Db type for box provisioning inbox");
        }
    }
}
