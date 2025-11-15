using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VnDocSign.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_ArchiveFields_To_DossierContent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NgayLuuTru",
                table: "DossierContents",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SoLuuTru",
                table: "DossierContents",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NgayLuuTru",
                table: "DossierContents");

            migrationBuilder.DropColumn(
                name: "SoLuuTru",
                table: "DossierContents");
        }
    }
}
