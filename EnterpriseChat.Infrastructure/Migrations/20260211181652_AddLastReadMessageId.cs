using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLastReadMessageId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChatUserId",
                table: "ChatRoomMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReadAt",
                table: "ChatRoomMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastReadMessageId",
                table: "ChatRoomMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Message_Content",
                table: "Messages",
                column: "Content");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_ChatUserId",
                table: "ChatRoomMembers",
                column: "ChatUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_LastReadMessageId",
                table: "ChatRoomMembers",
                column: "LastReadMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_RoomId_UserId",
                table: "ChatRoomMembers",
                columns: new[] { "RoomId", "UserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomMembers_Users_ChatUserId",
                table: "ChatRoomMembers",
                column: "ChatUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomMembers_Users_ChatUserId",
                table: "ChatRoomMembers");

            migrationBuilder.DropIndex(
                name: "IX_Message_Content",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_ChatRoomMembers_ChatUserId",
                table: "ChatRoomMembers");

            migrationBuilder.DropIndex(
                name: "IX_ChatRoomMembers_LastReadMessageId",
                table: "ChatRoomMembers");

            migrationBuilder.DropIndex(
                name: "IX_ChatRoomMembers_RoomId_UserId",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "ChatUserId",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "LastReadAt",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "LastReadMessageId",
                table: "ChatRoomMembers");
        }
    }
}
