using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VitalFace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "screenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeartRateBpm = table.Column<double>(type: "double precision", nullable: true),
                    RespiratoryRateBpm = table.Column<double>(type: "double precision", nullable: true),
                    FatigueScore = table.Column<double>(type: "double precision", nullable: false),
                    IsCritical = table.Column<bool>(type: "boolean", nullable: false),
                    CriticalReasons = table.Column<string>(type: "jsonb", nullable: false),
                    consent_signature_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    consent_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screenings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_screenings_CreatedAtUtc",
                table: "screenings",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_screenings_LocationId",
                table: "screenings",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "screenings");
        }
    }
}
