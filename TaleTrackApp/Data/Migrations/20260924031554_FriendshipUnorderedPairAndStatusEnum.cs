using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaleTrackApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class FriendshipUnorderedPairAndStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Friendships_RequesterId_AddresseeId",
                table: "Friendships");

            migrationBuilder.AddColumn<Guid>(
                name: "UserHighId",
                table: "Friendships",
                type: "uuid",
                nullable: false,
                computedColumnSql: "GREATEST(\"RequesterId\", \"AddresseeId\")",
                stored: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UserLowId",
                table: "Friendships",
                type: "uuid",
                nullable: false,
                computedColumnSql: "LEAST(\"RequesterId\", \"AddresseeId\")",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequesterId",
                table: "Friendships",
                column: "RequesterId");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_UserLowId_UserHighId",
                table: "Friendships",
                columns: new[] { "UserLowId", "UserHighId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Friendships_RequesterId",
                table: "Friendships");

            migrationBuilder.DropIndex(
                name: "IX_Friendships_UserLowId_UserHighId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "UserHighId",
                table: "Friendships");

            migrationBuilder.DropColumn(
                name: "UserLowId",
                table: "Friendships");

            migrationBuilder.CreateIndex(
                name: "IX_Friendships_RequesterId_AddresseeId",
                table: "Friendships",
                columns: new[] { "RequesterId", "AddresseeId" },
                unique: true);
        }
    }
}
