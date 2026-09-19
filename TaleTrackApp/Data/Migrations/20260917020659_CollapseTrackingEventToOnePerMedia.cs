using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaleTrackApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class CollapseTrackingEventToOnePerMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A series used to keep one row per (user, media, season, episode); now it's one
            // row per (user, media) holding the furthest (season, episode) reached. Collapse
            // any existing duplicates before the unique index below, keeping — per (user,
            // media) — the row with the greatest (season, episode), tie-broken by the most
            // recent EventDate (movies/books have no season/episode, so they only ever
            // compare on EventDate, and were already unique per (user, media) beforehand).
            migrationBuilder.Sql(
                """
                DELETE FROM "TrackingEvents" AS t
                WHERE EXISTS (
                    SELECT 1 FROM "TrackingEvents" AS t2
                    WHERE t2."UserId" = t."UserId"
                      AND t2."MediaId" = t."MediaId"
                      AND t2."Id" <> t."Id"
                      AND (
                        COALESCE(t2."Season", -1) > COALESCE(t."Season", -1)
                        OR (COALESCE(t2."Season", -1) = COALESCE(t."Season", -1) AND COALESCE(t2."Episode", -1) > COALESCE(t."Episode", -1))
                        OR (COALESCE(t2."Season", -1) = COALESCE(t."Season", -1) AND COALESCE(t2."Episode", -1) = COALESCE(t."Episode", -1) AND t2."EventDate" > t."EventDate")
                      )
                );
                """);

            migrationBuilder.DropIndex(
                name: "IX_TrackingEvents_UserId",
                table: "TrackingEvents");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingEvents_UserId_MediaId",
                table: "TrackingEvents",
                columns: new[] { "UserId", "MediaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackingEvents_UserId_MediaId",
                table: "TrackingEvents");

            migrationBuilder.CreateIndex(
                name: "IX_TrackingEvents_UserId",
                table: "TrackingEvents",
                column: "UserId");
        }
    }
}
