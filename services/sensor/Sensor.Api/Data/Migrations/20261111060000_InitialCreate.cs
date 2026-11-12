using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Sensor.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Moisture = table.Column<int>(type: "integer", nullable: true),
                    PumpOn = table.Column<bool>(type: "boolean", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    WifiRssi = table.Column<int>(type: "integer", nullable: true),
                    Firmware = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AutoWatering = table.Column<bool>(type: "boolean", nullable: false),
                    PendingPumpSeconds = table.Column<int>(type: "integer", nullable: true),
                    PendingPumpAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CommandSentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStoredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStoredMoisture = table.Column<int>(type: "integer", nullable: true),
                    LastDryAlertAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TankCheckRunId = table.Column<long>(type: "bigint", nullable: true),
                    TankCheckDueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TankEmpty = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Readings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Moisture = table.Column<int>(type: "integer", nullable: false),
                    PumpOn = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Readings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PumpRuns",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MoistureBefore = table.Column<int>(type: "integer", nullable: true),
                    MoistureAfter = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PumpRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_OwnerUserId",
                table: "Devices",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Readings_DeviceId_RecordedAtUtc",
                table: "Readings",
                columns: new[] { "DeviceId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PumpRuns_DeviceId_StartedAtUtc",
                table: "PumpRuns",
                columns: new[] { "DeviceId", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Readings");

            migrationBuilder.DropTable(
                name: "PumpRuns");
        }
    }
}
