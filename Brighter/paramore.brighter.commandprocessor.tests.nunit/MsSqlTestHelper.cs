using System;
using System.Data.SqlClient;
using paramore.brighter.commandprocessor.commandstore.mssql;

namespace paramore.brighter.commandprocessor.tests.nunit
{
    public class MsSqlTestHelper
    {
        public string ConnectionString;
        public string TableName = "test_messages";

        public SqlConnection CreateDatabase()
        {
            ConnectionString = $"Server={Server};Database={DatabaseName};Trusted_Connection=True;";
            return new SqlConnection(ConnectionString);
        }

        public string Server
        {
            get { throw new NotImplementedException(); }
        }

        public string DatabaseName
        {
            get { throw new NotImplementedException(); }
        }

        public MsSqlCommandStoreConfiguration Configuration
        {
            get
            {
                return new MsSqlCommandStoreConfiguration(ConnectionString, TableName,
                    MsSqlCommandStoreConfiguration.DatabaseType.MsSqlServer);
            }
        }

        public void CleanUpDb()
        {
            
        }
    }
}