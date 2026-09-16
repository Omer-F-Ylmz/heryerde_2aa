using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class CustomerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "customer_id",
                table: "product_review",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_id",
                table: "order",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "customer_id",
                table: "gift_registry",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    full_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: true),
                    email_verified_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    kvkk_consent_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    legal_version = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    marketing_consent = table.Column<bool>(type: "bit", nullable: false),
                    marketing_consent_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    session_stamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    verify_token_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    verify_token_expires_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    login_token_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    login_token_expires_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    reset_token_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    reset_token_expires_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer_address",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    title = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    city = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    district = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    is_default = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_address", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_address_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_favorite",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    customer_id = table.Column<int>(type: "int", nullable: false),
                    product_id = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_favorite", x => x.id);
                    table.ForeignKey(
                        name: "FK_customer_favorite_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_customer_favorite_product_product_id",
                        column: x => x.product_id,
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_review_customer_id",
                table: "product_review",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_customer_id",
                table: "order",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_gift_registry_customer_id",
                table: "gift_registry",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_phone",
                table: "customer",
                column: "phone");

            migrationBuilder.CreateIndex(
                name: "ux_customer_email",
                table: "customer",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_customer_login_token_hash",
                table: "customer",
                column: "login_token_hash",
                unique: true,
                filter: "[login_token_hash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_customer_reset_token_hash",
                table: "customer",
                column: "reset_token_hash",
                unique: true,
                filter: "[reset_token_hash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_customer_verify_token_hash",
                table: "customer",
                column: "verify_token_hash",
                unique: true,
                filter: "[verify_token_hash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_customer_address_customer_id",
                table: "customer_address",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "IX_customer_favorite_product_id",
                table: "customer_favorite",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_customer_favorite_customer_product",
                table: "customer_favorite",
                columns: new[] { "customer_id", "product_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_gift_registry_customer_customer_id",
                table: "gift_registry",
                column: "customer_id",
                principalTable: "customer",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_order_customer_customer_id",
                table: "order",
                column: "customer_id",
                principalTable: "customer",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_product_review_customer_customer_id",
                table: "product_review",
                column: "customer_id",
                principalTable: "customer",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_gift_registry_customer_customer_id",
                table: "gift_registry");

            migrationBuilder.DropForeignKey(
                name: "FK_order_customer_customer_id",
                table: "order");

            migrationBuilder.DropForeignKey(
                name: "FK_product_review_customer_customer_id",
                table: "product_review");

            migrationBuilder.DropTable(
                name: "customer_address");

            migrationBuilder.DropTable(
                name: "customer_favorite");

            migrationBuilder.DropTable(
                name: "customer");

            migrationBuilder.DropIndex(
                name: "ix_product_review_customer_id",
                table: "product_review");

            migrationBuilder.DropIndex(
                name: "ix_order_customer_id",
                table: "order");

            migrationBuilder.DropIndex(
                name: "ix_gift_registry_customer_id",
                table: "gift_registry");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "product_review");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "order");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "gift_registry");
        }
    }
}
