using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Coupons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "coupon_code",
                table: "order",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount",
                table: "order",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "coupon_code",
                table: "cart",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "coupon",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    min_subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    starts_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ends_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    total_limit = table.Column<int>(type: "int", nullable: true),
                    per_person_limit = table.Column<int>(type: "int", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupon", x => x.id);
                    table.CheckConstraint("ck_coupon_dates", "[ends_at] > [starts_at]");
                    table.CheckConstraint("ck_coupon_limits", "([total_limit] IS NULL OR [total_limit] >= 1) AND ([per_person_limit] IS NULL OR [per_person_limit] >= 1)");
                    table.CheckConstraint("ck_coupon_value", "[value] >= 0 AND [min_subtotal] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "ux_coupon_code",
                table: "coupon",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "coupon");

            migrationBuilder.DropColumn(
                name: "coupon_code",
                table: "order");

            migrationBuilder.DropColumn(
                name: "discount",
                table: "order");

            migrationBuilder.DropColumn(
                name: "coupon_code",
                table: "cart");
        }
    }
}
