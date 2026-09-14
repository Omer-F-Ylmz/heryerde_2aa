using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Shipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "carrier",
                table: "order",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tracking_no",
                table: "order",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "carrier",
                table: "order");

            migrationBuilder.DropColumn(
                name: "tracking_no",
                table: "order");
        }
    }
}
