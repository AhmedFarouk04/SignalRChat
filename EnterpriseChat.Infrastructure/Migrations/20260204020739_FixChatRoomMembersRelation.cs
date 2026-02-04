using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixChatRoomMembersRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatRoomMembers_ChatRooms_ChatRoomId",
                table: "ChatRoomMembers");

            migrationBuilder.DropIndex(
                name: "IX_ChatRoomMembers_ChatRoomId",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "ChatRoomId",
                table: "ChatRoomMembers");

            migrationBuilder.AlterColumn<bool>(
                name: "IsOwner",
                table: "ChatRoomMembers",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_CreatedAt",
                table: "Messages",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_CreatedAt",
                table: "Messages");

            migrationBuilder.AlterColumn<bool>(
                name: "IsOwner",
                table: "ChatRoomMembers",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ChatRoomId",
                table: "ChatRoomMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRoomMembers_ChatRoomId",
                table: "ChatRoomMembers",
                column: "ChatRoomId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRoomMembers_ChatRooms_ChatRoomId",
                table: "ChatRoomMembers",
                column: "ChatRoomId",
                principalTable: "ChatRooms",
                principalColumn: "Id");
        }
    }
}
