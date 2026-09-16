using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class DegerlendirmeDaveti : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "review_mail_at",
                table: "order",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_review_mail_at_delivered_at",
                table: "order",
                columns: new[] { "review_mail_at", "delivered_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_order_review_mail_at_delivered_at",
                table: "order");

            migrationBuilder.DropColumn(
                name: "review_mail_at",
                table: "order");
        }
    }
}
