using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace URLShortener.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesAndOptimizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add unique index on ShortCode for fast lookups
            migrationBuilder.CreateIndex(
                name: "IX_Urls_ShortCode",
                table: "Urls",
                column: "ShortCode",
                unique: true);

            // Add index on CreatedAt for time-based queries
            migrationBuilder.CreateIndex(
                name: "IX_Urls_CreatedAt",
                table: "Urls",
                column: "CreatedAt");

            // Add index on UserId for user-specific queries
            migrationBuilder.CreateIndex(
                name: "IX_Urls_UserId",
                table: "Urls",
                column: "UserId");

            // Add composite index for user queries with date filtering
            migrationBuilder.CreateIndex(
                name: "IX_Urls_UserId_CreatedAt",
                table: "Urls",
                columns: new[] { "UserId", "CreatedAt" });

            // Add index on IsActive and ExpiresAt for active URL queries
            migrationBuilder.CreateIndex(
                name: "IX_Urls_IsActive_ExpiresAt",
                table: "Urls",
                columns: new[] { "IsActive", "ExpiresAt" });

            // Analytics table indexes
            migrationBuilder.CreateIndex(
                name: "IX_Analytics_ShortCode_Timestamp",
                table: "Analytics",
                columns: new[] { "ShortCode", "Timestamp" });

            // Add index for geographic queries
            migrationBuilder.CreateIndex(
                name: "IX_Analytics_Country",
                table: "Analytics",
                column: "Country");

            // Add index for device type queries
            migrationBuilder.CreateIndex(
                name: "IX_Analytics_DeviceType",
                table: "Analytics",
                column: "DeviceType");

            // Composite index for analytics aggregation
            migrationBuilder.CreateIndex(
                name: "IX_Analytics_ShortCode_Country_DeviceType",
                table: "Analytics",
                columns: new[] { "ShortCode", "Country", "DeviceType" });

            // Events table indexes
            migrationBuilder.CreateIndex(
                name: "IX_Events_AggregateId_Version",
                table: "Events",
                columns: new[] { "AggregateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventType_OccurredAt",
                table: "Events",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Events_OccurredAt",
                table: "Events",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Urls_ShortCode",
                table: "Urls");

            migrationBuilder.DropIndex(
                name: "IX_Urls_CreatedAt",
                table: "Urls");

            migrationBuilder.DropIndex(
                name: "IX_Urls_UserId",
                table: "Urls");

            migrationBuilder.DropIndex(
                name: "IX_Urls_UserId_CreatedAt",
                table: "Urls");

            migrationBuilder.DropIndex(
                name: "IX_Urls_IsActive_ExpiresAt",
                table: "Urls");

            migrationBuilder.DropIndex(
                name: "IX_Analytics_ShortCode_Timestamp",
                table: "Analytics");

            migrationBuilder.DropIndex(
                name: "IX_Analytics_Country",
                table: "Analytics");

            migrationBuilder.DropIndex(
                name: "IX_Analytics_DeviceType",
                table: "Analytics");

            migrationBuilder.DropIndex(
                name: "IX_Analytics_ShortCode_Country_DeviceType",
                table: "Analytics");

            migrationBuilder.DropIndex(
                name: "IX_Events_AggregateId_Version",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_EventType_OccurredAt",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_OccurredAt",
                table: "Events");
        }
    }
}