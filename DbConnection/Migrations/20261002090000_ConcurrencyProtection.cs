using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DbConnection.Migrations;

[DbContext(typeof(DatabaseContext))]
[Migration("20261002090000_ConcurrencyProtection")]
public class ConcurrencyProtection : Migration
{
    private const string OldIdempotencyIndex = "IX_IdempotencyRecords_Id_Key_Operation_ReservationId_RequestHash_ResponseBody_ResponseStatusCode_ExpiresAt_CreatedAt";
    private const string NewIdempotencyIndex = "IX_IdempotencyRecords_Key_Operation_ReservationId_RequestHash_ResponseBody_ResponseStatusCode_ExpiresAt_CreatedAt";
    private static readonly string[] IdempotencyColumns =
        { "Key", "Operation", "ReservationId", "RequestHash", "ResponseBody", "ResponseStatusCode", "ExpiresAt", "CreatedAt" };

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Fail before changing schema rather than silently discard duplicate responses.
        migrationBuilder.Sql("""
            IF EXISTS (SELECT [Key] FROM [IdempotencyRecords] GROUP BY [Key] HAVING COUNT(*) > 1)
                THROW 50001, 'Resolve duplicate idempotency keys before applying ConcurrencyProtection.', 1;
            """);
        // Some databases were created from the updated Key-based model rather than
        // InitialCreate. Inspect the schema instead of assuming its old indexes exist.
        migrationBuilder.Sql($"""
            DROP INDEX IF EXISTS [{OldIdempotencyIndex}] ON [IdempotencyRecords];

            IF COL_LENGTH(N'IdempotencyRecords', N'Id') IS NOT NULL
            BEGIN
                IF EXISTS (SELECT 1 FROM sys.key_constraints
                           WHERE parent_object_id = OBJECT_ID(N'IdempotencyRecords')
                             AND name = N'PK_IdempotencyRecords')
                    ALTER TABLE [IdempotencyRecords] DROP CONSTRAINT [PK_IdempotencyRecords];
                ALTER TABLE [IdempotencyRecords] DROP COLUMN [Id];
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.key_constraints
                           WHERE parent_object_id = OBJECT_ID(N'IdempotencyRecords') AND type = 'PK')
                ALTER TABLE [IdempotencyRecords] ADD CONSTRAINT [PK_IdempotencyRecords] PRIMARY KEY ([Key]);

            IF NOT EXISTS (
                SELECT 1 FROM sys.key_constraints pk
                JOIN sys.index_columns ic ON ic.object_id = pk.parent_object_id
                    AND ic.index_id = pk.unique_index_id AND ic.key_ordinal > 0
                JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE pk.parent_object_id = OBJECT_ID(N'IdempotencyRecords')
                    AND pk.type = 'PK' AND c.name = N'Key'
                    AND NOT EXISTS (
                        SELECT 1 FROM sys.index_columns extra
                        WHERE extra.object_id = ic.object_id AND extra.index_id = ic.index_id
                            AND extra.key_ordinal > 1))
                THROW 50002, 'IdempotencyRecords must have a primary key on Key alone.', 1;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE object_id = OBJECT_ID(N'IdempotencyRecords')
                             AND name = N'{NewIdempotencyIndex}')
                CREATE INDEX [{NewIdempotencyIndex}] ON [IdempotencyRecords]
                    ([Key], [Operation], [ReservationId], [RequestHash], [ResponseBody],
                     [ResponseStatusCode], [ExpiresAt], [CreatedAt]);

            DROP INDEX IF EXISTS [IX_Reservations_Id_StartsAtUtc_EndsAtUtc_MeetingRoomId_Status] ON [Reservations];
            DROP INDEX IF EXISTS [IX_Reservations_MeetingRoomId] ON [Reservations];
            IF NOT EXISTS (SELECT 1 FROM sys.indexes
                           WHERE object_id = OBJECT_ID(N'Reservations')
                             AND name = N'IX_Reservations_MeetingRoomId_StartsAtUtc')
                CREATE INDEX [IX_Reservations_MeetingRoomId_StartsAtUtc]
                    ON [Reservations] ([MeetingRoomId], [StartsAtUtc]);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("IX_Reservations_MeetingRoomId_StartsAtUtc", "Reservations");
        migrationBuilder.CreateIndex("IX_Reservations_MeetingRoomId", "Reservations", "MeetingRoomId");
        migrationBuilder.CreateIndex("IX_Reservations_Id_StartsAtUtc_EndsAtUtc_MeetingRoomId_Status",
            "Reservations", new[] { "Id", "StartsAtUtc", "EndsAtUtc", "MeetingRoomId", "Status" });
        migrationBuilder.DropIndex(NewIdempotencyIndex, "IdempotencyRecords");
        migrationBuilder.DropPrimaryKey("PK_IdempotencyRecords", "IdempotencyRecords");
        migrationBuilder.AddColumn<Guid>("Id", "IdempotencyRecords", type: "uniqueidentifier",
            nullable: false, defaultValueSql: "NEWID()");
        migrationBuilder.AddPrimaryKey("PK_IdempotencyRecords", "IdempotencyRecords", "Id");
        migrationBuilder.CreateIndex(OldIdempotencyIndex, "IdempotencyRecords",
            new[] { "Id" }.Concat(IdempotencyColumns).ToArray());
    }
}
