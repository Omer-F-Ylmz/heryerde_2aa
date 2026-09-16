using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HerYerde.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Analytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "analytics_daily",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    day = table.Column<DateTime>(type: "date", nullable: false),
                    @event = table.Column<string>(name: "event", type: "nvarchar(20)", maxLength: 20, nullable: false),
                    path = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    source = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    device = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    views = table.Column<int>(type: "int", nullable: false),
                    visits = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_daily", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "page_view",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    @event = table.Column<string>(name: "event", type: "nvarchar(20)", maxLength: 20, nullable: false),
                    path = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    referrer_host = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    utm_source = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    utm_medium = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    utm_campaign = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    device = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    day = table.Column<DateTime>(type: "date", nullable: false),
                    hour = table.Column<byte>(type: "tinyint", nullable: false),
                    half_hour = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_page_view", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_analytics_daily_key",
                table: "analytics_daily",
                columns: new[] { "day", "event", "path", "source", "device" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_page_view_day_event",
                table: "page_view",
                columns: new[] { "day", "event" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_daily");

            migrationBuilder.DropTable(
                name: "page_view");
        }
    }
}
