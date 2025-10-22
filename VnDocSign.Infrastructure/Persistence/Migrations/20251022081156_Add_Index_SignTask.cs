using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VnDocSign.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Index_SignTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SignTasks_DossierId_SlotKey_Status_IsActivated",
                table: "SignTasks",
                columns: new[] { "DossierId", "SlotKey", "Status", "IsActivated" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SignTasks_DossierId_SlotKey_Status_IsActivated",
                table: "SignTasks");
        }
    }
}
