using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLastReactionPreviewToChatRoom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastReactionAt",
                table: "ChatRooms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastReactionPreview",
                table: "ChatRooms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastReactionTargetUserId",
                table: "ChatRooms",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastReactionAt",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "LastReactionPreview",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "LastReactionTargetUserId",
                table: "ChatRooms");
        }
    }
}
