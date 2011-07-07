//From Mark Nijhof
//http://github.com/MarkNijhof/Fohjin

using System.Data.Common;
using System.Data.SQLite;
using System.IO;

namespace Fohjin.DDD.Configuration
{
    public class DomainDatabaseBootStrapper
    {
        public const string dataBaseFile = "domainDataBase.db3";

        public static void BootStrap()
        {
            new DomainDatabaseBootStrapper().CreateDatabaseSchemaIfNeeded();
        }

        public void ReCreateDatabaseSchema()
        {
            if (File.Exists(dataBaseFile))
                File.Delete(dataBaseFile);

            DoCreateDatabaseSchema();            
        }

        public void CreateDatabaseSchemaIfNeeded()
        {
            if (File.Exists(dataBaseFile))
                return;

            DoCreateDatabaseSchema();
        }

        private static void DoCreateDatabaseSchema()
        {
            SQLiteConnection.CreateFile(dataBaseFile);

            var sqLiteConnection = new SQLiteConnection(string.Format("Data Source={0}", dataBaseFile));

            sqLiteConnection.Open();

            using (DbTransaction dbTrans = sqLiteConnection.BeginTransaction())
            {
                using (DbCommand sqLiteCommand = sqLiteConnection.CreateCommand())
                {
                    const string eventprovidersTables = @"
                        CREATE TABLE EventProviders
                        (
                            [EventProviderId] [uniqueidentifier] primary key,
                            [Type] [nvarchar(250)] not null,
                            [Version] [int] not null
                        );
                        ";
                    sqLiteCommand.CommandText = eventprovidersTables;
                    sqLiteCommand.ExecuteNonQuery();

                    const string eventsTables = @"
                        CREATE TABLE Events
                        (
                            [Id] [uniqueidentifier] primary key,
                            [EventProviderId] [uniqueidentifier] not null,
                            [Event] [binary] not null,
                            [Version] [int] not null
                        );
                        ";
                    sqLiteCommand.CommandText = eventsTables;
                    sqLiteCommand.ExecuteNonQuery();

                    const string snapshotsTables = @"
                        CREATE TABLE SnapShots
                        (
                            [EventProviderId] [uniqueidentifier] primary key,
                            [SnapShot] [binary] not null,
                            [Version] [int] not null
                        );
                        ";
                    sqLiteCommand.CommandText = snapshotsTables;
                    sqLiteCommand.ExecuteNonQuery();
                }
                dbTrans.Commit();
            }

            sqLiteConnection.Close();
        }
    }
}