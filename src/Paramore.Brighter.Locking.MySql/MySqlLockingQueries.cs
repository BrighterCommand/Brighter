namespace Paramore.Brighter.Locking.MySql;

public static class MySqlLockingQueries
{
    public const string ObtainLockQuery = "SELECT GET_LOCK(@RESOURCE_NAME, @TIMEOUT)";

    public const string ReleaseLockQuery = "SELECT RELEASE_LOCK(@RESOURCE_NAME)";
}
