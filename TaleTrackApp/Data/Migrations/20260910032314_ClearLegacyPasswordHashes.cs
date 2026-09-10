using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaleTrackApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClearLegacyPasswordHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Passwords were previously stored as unsalted Base64 SHA-256 (a fixed 44-char
            // string). Those hashes can't be verified against the new PBKDF2 scheme and
            // can't be converted, so drop them. Affected users log in via email code and
            // set a new password from their profile.
            migrationBuilder.Sql(
                "UPDATE \"Users\" SET \"PasswordHash\" = NULL WHERE length(\"PasswordHash\") = 44;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible: the original hashes are gone.
        }
    }
}
