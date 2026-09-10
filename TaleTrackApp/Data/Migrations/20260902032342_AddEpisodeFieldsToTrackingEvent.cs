using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaleTrackApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEpisodeFieldsToTrackingEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Episode",
                table: "TrackingEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EpisodeTitle",
                table: "TrackingEvents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Season",
                table: "TrackingEvents",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Episode",
                table: "TrackingEvents");

            migrationBuilder.DropColumn(
                name: "EpisodeTitle",
                table: "TrackingEvents");

            migrationBuilder.DropColumn(
                name: "Season",
                table: "TrackingEvents");
        }
    }
}
