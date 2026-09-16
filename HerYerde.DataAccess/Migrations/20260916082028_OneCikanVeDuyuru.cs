using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class OneCikanVeDuyuru : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "featured_order",
                table: "product",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_featured",
                table: "product",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "announcement",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    text = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    starts_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    color = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcement", x => x.id);
                    table.CheckConstraint("ck_announcement_dates", "[ends_at] > [starts_at]");
                });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_is_active_starts_at_ends_at",
                table: "announcement",
                columns: new[] { "is_active", "starts_at", "ends_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement");

            migrationBuilder.DropColumn(
                name: "featured_order",
                table: "product");

            migrationBuilder.DropColumn(
                name: "is_featured",
                table: "product");
        }
    }
}
