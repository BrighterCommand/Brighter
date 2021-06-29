using GreetingsSender.Web.Data;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GreetingsSender.Web.Migrations
{
    public partial class BrighterOutbox : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            MigrationSqlHelper.ApplyResource(this, migrationBuilder, "Scripts.CreateBrighterOutbox.sql");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE [BrighterOutbox]");
        }
    }
}
