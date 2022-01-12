namespace Paramore.Brighter.PostgreSql
{
    public abstract class PostgreSqlConfiguration
    {
        protected PostgreSqlConfiguration(string connectionString)
        {
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }
    }
}
