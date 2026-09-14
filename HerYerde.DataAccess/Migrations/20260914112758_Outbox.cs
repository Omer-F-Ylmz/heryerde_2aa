using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Outbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "seen_at",
                table: "order",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "outbox_message",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    to = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    try_count = table.Column<int>(type: "int", nullable: false),
                    next_try_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    sent_at = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_message", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_status_next_try_at",
                table: "outbox_message",
                columns: new[] { "status", "next_try_at" });

            // Sütun eklenmeden önceki siparişler zaten yönetimde görülmüştü; aksi halde
            // sürümü alan mağaza tüm geçmişi "yeni" rozetiyle görürdü.
            migrationBuilder.Sql("UPDATE [order] SET [seen_at] = [created_at];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_message");

            migrationBuilder.DropColumn(
                name: "seen_at",
                table: "order");
        }
    }
}
