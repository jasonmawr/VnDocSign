using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VnDocSign.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleNameToUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoleName",
                table: "UserRoles",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoleName",
                table: "UserRoles");
        }
    }
}
