using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaleTrackApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailCode",
                table: "Users",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailCodeExpiry",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailCodeExpiry",
                table: "Users");
        }
    }
}
