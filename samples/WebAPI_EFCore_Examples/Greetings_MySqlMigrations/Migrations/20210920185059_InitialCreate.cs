using Microsoft.EntityFrameworkCore.Migrations;

namespace Greetings_MySqlMigrations.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "People",
                columns: table => new
                {
                    _id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeStamp = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_People", x => x._id);
                    table.UniqueConstraint("AK_People_Name", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Greeting",
                columns: table => new
                {
                    _id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Recipient_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Greeting", x => x._id);
                    table.ForeignKey(
                        name: "FK_Greeting_People_Recipient_id",
                        column: x => x.Recipient_id,
                        principalTable: "People",
                        principalColumn: "_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Greeting_Recipient_id",
                table: "Greeting",
                column: "Recipient_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Greeting");

            migrationBuilder.DropTable(
                name: "People");
        }
    }
}
