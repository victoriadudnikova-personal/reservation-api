using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DbConnection.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIdempotencyRecordId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_IdempotencyRecords",
                table: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_Id_Key_Operation_ReservationId_RequestHash_ResponseBody_ResponseStatusCode_ExpiresAt_CreatedAt",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "IdempotencyRecords");

            migrationBuilder.AddPrimaryKey(
                name: "PK_IdempotencyRecords",
                table: "IdempotencyRecords",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Key_Operation_ReservationId_RequestHash_ResponseBody_ResponseStatusCode_ExpiresAt_CreatedAt",
                table: "IdempotencyRecords",
                columns: new[] { "Key", "Operation", "ReservationId", "RequestHash", "ResponseBody", "ResponseStatusCode", "ExpiresAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_IdempotencyRecords",
                table: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_Key_Operation_ReservationId_RequestHash_ResponseBody_ResponseStatusCode_ExpiresAt_CreatedAt",
                table: "IdempotencyRecords");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "IdempotencyRecords",
                type: "uniqueidentifier",
                maxLength: 36,
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_IdempotencyRecords",
                table: "IdempotencyRecords",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_Id_Key_Operation_ReservationId_RequestHash_ResponseBody_ResponseStatusCode_ExpiresAt_CreatedAt",
                table: "IdempotencyRecords",
                columns: new[] { "Id", "Key", "Operation", "ReservationId", "RequestHash", "ResponseBody", "ResponseStatusCode", "ExpiresAt", "CreatedAt" });
        }
    }
}
