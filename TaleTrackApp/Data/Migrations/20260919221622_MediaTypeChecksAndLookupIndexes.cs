using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaleTrackApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class MediaTypeChecksAndLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Medias_Isbn",
                table: "Medias",
                column: "Isbn",
                filter: "\"Isbn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_TitleEN",
                table: "Medias",
                column: "TitleEN");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_TitleES",
                table: "Medias",
                column: "TitleES");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Medias_Author_BookOnly",
                table: "Medias",
                sql: "\"Author\" IS NULL OR \"Type\" = 'Book'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Medias_Isbn_BookOnly",
                table: "Medias",
                sql: "\"Isbn\" IS NULL OR \"Type\" = 'Book'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Medias_SeasonEpisodeCounts_SeriesOnly",
                table: "Medias",
                sql: "\"SeasonEpisodeCounts\" IS NULL OR \"Type\" = 'Series'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Medias_Type",
                table: "Medias",
                sql: "\"Type\" IN ('Movie', 'Series', 'Book')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Medias_Isbn",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_TitleEN",
                table: "Medias");

            migrationBuilder.DropIndex(
                name: "IX_Medias_TitleES",
                table: "Medias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Medias_Author_BookOnly",
                table: "Medias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Medias_Isbn_BookOnly",
                table: "Medias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Medias_SeasonEpisodeCounts_SeriesOnly",
                table: "Medias");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Medias_Type",
                table: "Medias");
        }
    }
}
