using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class GiftRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "gift_registry_item_id",
                table: "order_item",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "gift_registry_item_id",
                table: "cart_item",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "gift_registry",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    slug = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    manage_token = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    owner_name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    event_date = table.Column<DateTime>(type: "date", nullable: false),
                    message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    is_public = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_registry", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gift_registry_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    gift_registry_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    variant_id = table.Column<int>(type: "int", nullable: true),
                    desired_qty = table.Column<int>(type: "int", nullable: false),
                    received_qty = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_registry_item", x => x.id);
                    table.CheckConstraint("ck_gift_registry_item_desired_qty", "[desired_qty] BETWEEN 1 AND 99");
                    table.CheckConstraint("ck_gift_registry_item_received_qty", "[received_qty] >= 0");
                    table.ForeignKey(
                        name: "FK_gift_registry_item_gift_registry_gift_registry_id",
                        column: x => x.gift_registry_id,
                        principalTable: "gift_registry",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gift_registry_item_product_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_gift_registry_item_product_variant_variant_id",
                        column: x => x.variant_id,
                        principalTable: "product_variant",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_cart_item_gift_registry_item_id",
                table: "cart_item",
                column: "gift_registry_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_registry_event_date",
                table: "gift_registry",
                column: "event_date");

            migrationBuilder.CreateIndex(
                name: "ux_gift_registry_manage_token",
                table: "gift_registry",
                column: "manage_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_gift_registry_slug",
                table: "gift_registry",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gift_registry_item_product_id",
                table: "gift_registry_item",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_gift_registry_item_variant_id",
                table: "gift_registry_item",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ux_gift_registry_item_gift_registry_id_product_id_variant_id",
                table: "gift_registry_item",
                columns: new[] { "gift_registry_id", "product_id", "variant_id" },
                unique: true,
                filter: "[variant_id] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_cart_item_gift_registry_item_gift_registry_item_id",
                table: "cart_item",
                column: "gift_registry_item_id",
                principalTable: "gift_registry_item",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cart_item_gift_registry_item_gift_registry_item_id",
                table: "cart_item");

            migrationBuilder.DropTable(
                name: "gift_registry_item");

            migrationBuilder.DropTable(
                name: "gift_registry");

            migrationBuilder.DropIndex(
                name: "IX_cart_item_gift_registry_item_id",
                table: "cart_item");

            migrationBuilder.DropColumn(
                name: "gift_registry_item_id",
                table: "order_item");

            migrationBuilder.DropColumn(
                name: "gift_registry_item_id",
                table: "cart_item");
        }
    }
}
