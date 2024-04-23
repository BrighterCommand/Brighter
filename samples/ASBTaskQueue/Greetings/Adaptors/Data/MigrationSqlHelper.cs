using System;
using System.IO;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GreetingsSender.Web.Data
{
    public static class MigrationSqlHelper
    {
        public static void ApplyResource(Migration migration, MigrationBuilder migrationBuilder, string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            var name = $"{migration.GetType().Namespace}.{resourceName}";
            using var stream = assembly.GetManifestResourceStream(name);
            if (stream == null) throw new ArgumentNullException(resourceName);
            using var textStreamReader = new StreamReader(stream);
            var sql = textStreamReader.ReadToEnd();
            migrationBuilder.Sql(sql);
        }
    }
}
