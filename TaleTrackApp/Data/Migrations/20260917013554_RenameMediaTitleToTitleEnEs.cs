using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaleTrackApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameMediaTitleToTitleEnEs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve existing data: the old single Title becomes TitleEN (the default
            // bucket for content whose language wasn't recorded), AltTitle becomes TitleES.
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "Medias",
                newName: "TitleEN");

            migrationBuilder.AlterColumn<string>(
                name: "TitleEN",
                table: "Medias",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.RenameColumn(
                name: "AltTitle",
                table: "Medias",
                newName: "TitleES");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TitleES",
                table: "Medias",
                newName: "AltTitle");

            migrationBuilder.AlterColumn<string>(
                name: "TitleEN",
                table: "Medias",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "TitleEN",
                table: "Medias",
                newName: "Title");
        }
    }
}
