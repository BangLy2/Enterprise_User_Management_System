using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyWeb.Migrations
{
    /// <inheritdoc />
    public partial class AddChangesJsonToAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChangesJson",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChangesJson",
                table: "AuditLogs");
        }
    }
}
