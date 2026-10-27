using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dhathuru.Bookings.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBookings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "bookings");

            migrationBuilder.CreateTable(
                name: "bookings",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OptionId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    TotalMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    HoldExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreateKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreateRequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PriceLinesJson = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bookings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PaymentId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refunds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Actor = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_entries_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_legs",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LegIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    LegJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ServiceRunId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Bookable = table.Column<bool>(type: "boolean", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    HoldReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ProviderReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RefundedMinor = table.Column<long>(type: "bigint", nullable: false),
                    HoldReleased = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_legs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_legs_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_passengers",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FullName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TravellerId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_passengers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_passengers_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "disruptions",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingLegId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LegIndex = table.Column<int>(type: "integer", nullable: false),
                    Cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuggestionJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_disruptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_disruptions_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Gateway = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    GatewayReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ChallengeId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RefundedMinor = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payments_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                schema: "bookings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingLegId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PassengerId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Nonce = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    KeyId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tickets_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "bookings",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_BookingId_At",
                schema: "bookings",
                table: "audit_entries",
                columns: new[] { "BookingId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_legs_BookingId_LegIndex",
                schema: "bookings",
                table: "booking_legs",
                columns: new[] { "BookingId", "LegIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_booking_legs_ProviderId_ServiceRunId",
                schema: "bookings",
                table: "booking_legs",
                columns: new[] { "ProviderId", "ServiceRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_passengers_BookingId",
                schema: "bookings",
                table: "booking_passengers",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_Status_HoldExpiresAt",
                schema: "bookings",
                table: "bookings",
                columns: new[] { "Status", "HoldExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId_CreateKey",
                schema: "bookings",
                table: "bookings",
                columns: new[] { "UserId", "CreateKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_bookings_UserId_CreatedAt",
                schema: "bookings",
                table: "bookings",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_disruptions_BookingId",
                schema: "bookings",
                table: "disruptions",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_disruptions_BookingLegId_State",
                schema: "bookings",
                table: "disruptions",
                columns: new[] { "BookingLegId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_payments_BookingId_Attempt",
                schema: "bookings",
                table: "payments",
                columns: new[] { "BookingId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_BookingId_IdempotencyKey",
                schema: "bookings",
                table: "payments",
                columns: new[] { "BookingId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refunds_BookingId",
                schema: "bookings",
                table: "refunds",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_refunds_IdempotencyKey",
                schema: "bookings",
                table: "refunds",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tickets_BookingId",
                schema: "bookings",
                table: "tickets",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_tickets_BookingLegId_PassengerId",
                schema: "bookings",
                table: "tickets",
                columns: new[] { "BookingLegId", "PassengerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "booking_legs",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "booking_passengers",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "disruptions",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "refunds",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "tickets",
                schema: "bookings");

            migrationBuilder.DropTable(
                name: "bookings",
                schema: "bookings");
        }
    }
}
