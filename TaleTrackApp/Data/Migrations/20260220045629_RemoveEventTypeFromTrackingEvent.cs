using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaleTrackApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEventTypeFromTrackingEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EventType",
                table: "TrackingEvents");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "TrackingEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "TrackingEvents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "TrackingEvents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }
    }
}
