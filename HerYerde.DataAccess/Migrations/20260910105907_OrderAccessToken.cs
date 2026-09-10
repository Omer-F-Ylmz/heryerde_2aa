using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class OrderAccessToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "access_token",
                table: "order",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Mevcut siparişler de tekil bir anahtar alsın; aksi halde tekil dizin kurulamaz.
            migrationBuilder.Sql("UPDATE [order] SET [access_token] = NEWID();");

            migrationBuilder.CreateIndex(
                name: "ux_order_access_token",
                table: "order",
                column: "access_token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_order_access_token",
                table: "order");

            migrationBuilder.DropColumn(
                name: "access_token",
                table: "order");
        }
    }
}
