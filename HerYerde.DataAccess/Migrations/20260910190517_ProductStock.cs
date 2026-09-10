using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ProductStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gift_mode",
                table: "product",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "gift_product_id",
                table: "product",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "gift_qty",
                table: "product",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "stock",
                table: "product",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_gift",
                table: "order_item",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "password_changed_at",
                table: "admin_user",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_product_gift_product_id",
                table: "product",
                column: "gift_product_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_gift",
                table: "product",
                sql: "([gift_mode] = 2 AND [gift_product_id] IS NOT NULL) OR ([gift_mode] <> 2 AND [gift_product_id] IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_gift_qty",
                table: "product",
                sql: "[gift_qty] >= 1");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_stock",
                table: "product",
                sql: "[stock] IS NULL OR [stock] >= 0");

            migrationBuilder.AddForeignKey(
                name: "FK_product_product_gift_product_id",
                table: "product",
                column: "gift_product_id",
                principalTable: "product",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_product_product_gift_product_id",
                table: "product");

            migrationBuilder.DropIndex(
                name: "IX_product_gift_product_id",
                table: "product");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_gift",
                table: "product");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_gift_qty",
                table: "product");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_stock",
                table: "product");

            migrationBuilder.DropColumn(
                name: "gift_mode",
                table: "product");

            migrationBuilder.DropColumn(
                name: "gift_product_id",
                table: "product");

            migrationBuilder.DropColumn(
                name: "gift_qty",
                table: "product");

            migrationBuilder.DropColumn(
                name: "stock",
                table: "product");

            migrationBuilder.DropColumn(
                name: "is_gift",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "password_changed_at",
                table: "admin_user");
        }
    }
}
