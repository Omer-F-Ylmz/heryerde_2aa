using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class OrderSourceInvoiceNoticeAdminSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "invoice_date",
                table: "order",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_file",
                table: "order",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invoice_no",
                table: "order",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "order",
                type: "int",
                nullable: false,
                // Önceki siparişlerin hepsi vitrinden: OrderSource.Site.
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                table: "admin_user",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "recovery_code_hashes",
                table: "admin_user",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reset_token_expires_at",
                table: "admin_user",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reset_token_hash",
                table: "admin_user",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "totp_enabled",
                table: "admin_user",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "totp_secret",
                table: "admin_user",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "detail",
                table: "admin_audit_log",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "payment_notice",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    order_id = table.Column<int>(type: "int", nullable: false),
                    sender_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    paid_on = table.Column<DateTime>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    receipt_file = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    approved_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_notice", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_notice_order_order_id",
                        column: x => x.order_id,
                        principalTable: "order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_payment_notice_order_id",
                table: "payment_notice",
                column: "order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payment_notice");

            migrationBuilder.DropColumn(
                name: "invoice_date",
                table: "order");

            migrationBuilder.DropColumn(
                name: "invoice_file",
                table: "order");

            migrationBuilder.DropColumn(
                name: "invoice_no",
                table: "order");

            migrationBuilder.DropColumn(
                name: "source",
                table: "order");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                table: "admin_user");

            migrationBuilder.DropColumn(
                name: "recovery_code_hashes",
                table: "admin_user");

            migrationBuilder.DropColumn(
                name: "reset_token_expires_at",
                table: "admin_user");

            migrationBuilder.DropColumn(
                name: "reset_token_hash",
                table: "admin_user");

            migrationBuilder.DropColumn(
                name: "totp_enabled",
                table: "admin_user");

            migrationBuilder.DropColumn(
                name: "totp_secret",
                table: "admin_user");

            migrationBuilder.DropColumn(
                name: "detail",
                table: "admin_audit_log");
        }
    }
}
