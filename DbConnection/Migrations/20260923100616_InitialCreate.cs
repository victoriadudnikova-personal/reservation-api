using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DbConnection.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MeetingRooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingRooms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", maxLength: 36, nullable: false),
                    Status = table.Column<int>(type: "int", maxLength: 1, nullable: false),
                    StartsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndsAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MeetingRoomId = table.Column<Guid>(type: "uniqueidentifier", maxLength: 36, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservations_MeetingRooms_MeetingRoomId",
                        column: x => x.MeetingRoomId,
                        principalTable: "MeetingRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", maxLength: 36, nullable: false),
                    Key = table.Column<Guid>(type: "uniqueidentifier", maxLength: 36, nullable: false),
                    Operation = table.Column<int>(type: "int", maxLength: 1, nullable: false),
                    RequestHash = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", maxLength: 3, nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdempotencyRecords_Reservations_ReservationId",
                        column: x => x.ReservationId,
                        principalTable: "Reservations",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "MeetingRooms",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { new Guid("242c2a00-ae11-46cd-bb0c-b66176f4b569"), "Room 4" },
                    { new Guid("312c5953-b9e5-471c-b7bf-fa1d6650f5b1"), "Room 3" },
                    { new Guid("79b5947f-a1aa-49f0-b4b6-5bb23d070c12"), "Room 2" },
                    { new Guid("847b786f-de7d-47eb-9f0e-843062e436ef"), "Room 1" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Id_Key_Operation_ReservationId_RequestHash_ResponseBody_ResponseStatusCode_ExpiresAt_CreatedAt",
                table: "IdempotencyRecords",
                columns: new[] { "Id", "Key", "Operation", "ReservationId", "RequestHash", "ResponseBody", "ResponseStatusCode", "ExpiresAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ReservationId",
                table: "IdempotencyRecords",
                column: "ReservationId");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingRooms_Id_Name",
                table: "MeetingRooms",
                columns: new[] { "Id", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_Id_StartsAtUtc_EndsAtUtc_MeetingRoomId_Status",
                table: "Reservations",
                columns: new[] { "Id", "StartsAtUtc", "EndsAtUtc", "MeetingRoomId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_MeetingRoomId",
                table: "Reservations",
                column: "MeetingRoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "MeetingRooms");
        }
    }
}
