using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Dhathuru.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "min_transfers",
                schema: "catalog",
                columns: table => new
                {
                    PlaceId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FromMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ToMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Minutes = table.Column<int>(type: "integer", nullable: false),
                    Verified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_min_transfers", x => new { x.PlaceId, x.FromMode, x.ToMode });
                });

            migrationBuilder.CreateTable(
                name: "model_params",
                schema: "catalog",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    JsonValue = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_model_params", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "operators",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NamesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Modes = table.Column<string[]>(type: "text[]", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    ProviderKind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Website = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operators", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "place_links",
                schema: "catalog",
                columns: table => new
                {
                    FromId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ToId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DurationMin = table.Column<int>(type: "integer", nullable: false),
                    FareMinor = table.Column<long>(type: "bigint", nullable: false),
                    Pricing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Approximate = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_place_links", x => new { x.FromId, x.ToId, x.Mode });
                });

            migrationBuilder.CreateTable(
                name: "places",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NamesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Location = table.Column<Point>(type: "geometry(Point,4326)", nullable: false),
                    AtollId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ParentId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    IsHub = table.Column<bool>(type: "boolean", nullable: false),
                    Approximate = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ZoneKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_places", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reliability_priors",
                schema: "catalog",
                columns: table => new
                {
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OnTimeRate = table.Column<double>(type: "double precision", nullable: false),
                    P90DelayMin = table.Column<double>(type: "double precision", nullable: false),
                    Samples = table.Column<int>(type: "integer", nullable: false),
                    Synthetic = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reliability_priors", x => x.Mode);
                });

            migrationBuilder.CreateTable(
                name: "services",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OperatorId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FromId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ToId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    DurationMin = table.Column<int>(type: "integer", nullable: false),
                    FareMinor = table.Column<long>(type: "bigint", nullable: false),
                    Pricing = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CheckInCutoffMin = table.Column<int>(type: "integer", nullable: false),
                    CutoffVerified = table.Column<bool>(type: "boolean", nullable: false),
                    DaysOfWeek = table.Column<int[]>(type: "integer[]", nullable: false),
                    Times = table.Column<string[]>(type: "text[]", nullable: false),
                    FrequencyFirst = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    FrequencyLast = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    FrequencyHeadwayMin = table.Column<int>(type: "integer", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    Synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    OnDemand = table.Column<bool>(type: "boolean", nullable: false),
                    ChildDiscountPercent = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "taxi_durations",
                schema: "catalog",
                columns: table => new
                {
                    FromZone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ToZone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Band = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Samples = table.Column<int>(type: "integer", nullable: false),
                    MedianMin = table.Column<double>(type: "double precision", nullable: false),
                    P90Min = table.Column<double>(type: "double precision", nullable: false),
                    MedianKm = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxi_durations", x => new { x.FromZone, x.ToZone, x.Band });
                });

            migrationBuilder.CreateTable(
                name: "taxi_fares",
                schema: "catalog",
                columns: table => new
                {
                    FromZone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ToZone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FareMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_taxi_fares", x => new { x.FromZone, x.ToZone });
                });

            migrationBuilder.CreateIndex(
                name: "IX_operators_Slug",
                schema: "catalog",
                table: "operators",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_places_Location",
                schema: "catalog",
                table: "places",
                column: "Location")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_places_Slug",
                schema: "catalog",
                table: "places",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_places_Type",
                schema: "catalog",
                table: "places",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_services_FromId_ToId",
                schema: "catalog",
                table: "services",
                columns: new[] { "FromId", "ToId" });

            migrationBuilder.CreateIndex(
                name: "IX_services_Slug",
                schema: "catalog",
                table: "services",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "min_transfers",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "model_params",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "operators",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "place_links",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "places",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "reliability_priors",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "services",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "taxi_durations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "taxi_fares",
                schema: "catalog");
        }
    }
}
