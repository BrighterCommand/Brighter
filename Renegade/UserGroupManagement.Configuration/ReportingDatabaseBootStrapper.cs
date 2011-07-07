//From Mark Nijhof
//http://github.com/MarkNijhof/Fohjin


using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using Fohjin.DDD.Reporting.Infrastructure;
using UserGroupManagement.Reporting.Dto;

namespace UserGroupManagement.Configuration
{
    public class ReportingDatabaseBootStrapper
    {
        public const string DATA_BASE_FILE = "reportingDataBase.db3";
        private readonly List<Type> dtos = new List<Type>
        {
            typeof(MeetingReport), 
            typeof(MeetingDetailsReport),
        };
        private readonly SqlCreateBuilder sqlCreateBuilder = new SqlCreateBuilder();

        public static void BootStrap()
        {
            new ReportingDatabaseBootStrapper().CreateDatabaseSchemaIfNeeded();
        }

        public void ReCreateDatabaseSchema()
        {
            if (File.Exists(DATA_BASE_FILE))
                File.Delete(DATA_BASE_FILE);

            DoCreateDatabaseSchema();            
        }

        public void CreateDatabaseSchemaIfNeeded()
        {
            if (File.Exists(DATA_BASE_FILE))
                return;

            DoCreateDatabaseSchema();
        }

        private void DoCreateDatabaseSchema()
        {
            SQLiteConnection.CreateFile(DATA_BASE_FILE);

            var sqLiteConnection = new SQLiteConnection(string.Format("Data Source={0}", DATA_BASE_FILE));

            sqLiteConnection.Open();

            using (DbTransaction dbTrans = sqLiteConnection.BeginTransaction())
            {
                using (DbCommand sqLiteCommand = sqLiteConnection.CreateCommand())
                {
                    foreach (var dto in dtos)
                    {
                        sqLiteCommand.CommandText = sqlCreateBuilder.CreateSqlCreateStatementFromDto(dto);
                        sqLiteCommand.ExecuteNonQuery();
                    }
                }
                dbTrans.Commit();
            }

            sqLiteConnection.Close();
        }
    }
}