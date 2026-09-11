using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class LegalConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "consent_at",
                table: "order",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legal_version",
                table: "order",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consent_at",
                table: "order");

            migrationBuilder.DropColumn(
                name: "legal_version",
                table: "order");
        }
    }
}
