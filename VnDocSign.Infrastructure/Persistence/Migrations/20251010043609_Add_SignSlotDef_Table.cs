using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VnDocSign.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_SignSlotDef_Table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSignature_Users_UserId",
                table: "UserSignature");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSignature",
                table: "UserSignature");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SystemConfig",
                table: "SystemConfig");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SignSlotDef",
                table: "SignSlotDef");

            migrationBuilder.RenameTable(
                name: "UserSignature",
                newName: "UserSignatures");

            migrationBuilder.RenameTable(
                name: "SystemConfig",
                newName: "SystemConfigs");

            migrationBuilder.RenameTable(
                name: "SignSlotDef",
                newName: "SignSlotDefs");

            migrationBuilder.RenameIndex(
                name: "IX_UserSignature_UserId",
                table: "UserSignatures",
                newName: "IX_UserSignatures_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_SystemConfig_Slot",
                table: "SystemConfigs",
                newName: "IX_SystemConfigs_Slot");

            migrationBuilder.RenameIndex(
                name: "IX_SignSlotDef_Key",
                table: "SignSlotDefs",
                newName: "IX_SignSlotDefs_Key");

            migrationBuilder.AddColumn<string>(
                name: "VisiblePattern",
                table: "SignTasks",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSignatures",
                table: "UserSignatures",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SystemConfigs",
                table: "SystemConfigs",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SignSlotDefs",
                table: "SignSlotDefs",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSignatures_Users_UserId",
                table: "UserSignatures",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserSignatures_Users_UserId",
                table: "UserSignatures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserSignatures",
                table: "UserSignatures");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SystemConfigs",
                table: "SystemConfigs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SignSlotDefs",
                table: "SignSlotDefs");

            migrationBuilder.DropColumn(
                name: "VisiblePattern",
                table: "SignTasks");

            migrationBuilder.RenameTable(
                name: "UserSignatures",
                newName: "UserSignature");

            migrationBuilder.RenameTable(
                name: "SystemConfigs",
                newName: "SystemConfig");

            migrationBuilder.RenameTable(
                name: "SignSlotDefs",
                newName: "SignSlotDef");

            migrationBuilder.RenameIndex(
                name: "IX_UserSignatures_UserId",
                table: "UserSignature",
                newName: "IX_UserSignature_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_SystemConfigs_Slot",
                table: "SystemConfig",
                newName: "IX_SystemConfig_Slot");

            migrationBuilder.RenameIndex(
                name: "IX_SignSlotDefs_Key",
                table: "SignSlotDef",
                newName: "IX_SignSlotDef_Key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserSignature",
                table: "UserSignature",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SystemConfig",
                table: "SystemConfig",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SignSlotDef",
                table: "SignSlotDef",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserSignature_Users_UserId",
                table: "UserSignature",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
