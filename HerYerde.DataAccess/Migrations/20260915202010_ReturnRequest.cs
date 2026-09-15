using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ReturnRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attachment",
                table: "outbox_message",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "delivered_at",
                table: "order",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "refund_due",
                table: "order",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "refund_iban",
                table: "order",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refunded_at",
                table: "order",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "return_request",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_id = table.Column<int>(type: "int", nullable: false),
                    type = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    photo_file = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    refund_iban = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reject_reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    received_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    refund_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    refunded_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_request", x => x.id);
                    table.ForeignKey(
                        name: "FK_return_request_order_order_id",
                        column: x => x.order_id,
                        principalTable: "order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "return_request_item",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    return_request_id = table.Column<int>(type: "int", nullable: false),
                    order_item_id = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    new_sku = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_return_request_item", x => x.id);
                    table.CheckConstraint("ck_return_request_item_quantity", "[quantity] >= 1");
                    table.ForeignKey(
                        name: "FK_return_request_item_order_item_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_item",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_return_request_item_return_request_return_request_id",
                        column: x => x.return_request_id,
                        principalTable: "return_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_return_request_order_id",
                table: "return_request",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_return_request_status_created_at",
                table: "return_request",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_return_request_item_order_item_id",
                table: "return_request_item",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_return_request_item_return_request_id",
                table: "return_request_item",
                column: "return_request_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "return_request_item");

            migrationBuilder.DropTable(
                name: "return_request");

            migrationBuilder.DropColumn(
                name: "attachment",
                table: "outbox_message");

            migrationBuilder.DropColumn(
                name: "delivered_at",
                table: "order");

            migrationBuilder.DropColumn(
                name: "refund_due",
                table: "order");

            migrationBuilder.DropColumn(
                name: "refund_iban",
                table: "order");

            migrationBuilder.DropColumn(
                name: "refunded_at",
                table: "order");
        }
    }
}
