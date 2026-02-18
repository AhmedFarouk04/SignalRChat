using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageReadStatusFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageReceipts_UserId_Status",
                table: "MessageReceipts");

            migrationBuilder.AddColumn<Guid>(
                name: "RoomId",
                table: "MessageReceipts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageAt",
                table: "ChatRooms",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMessageId",
                table: "ChatRooms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastMessagePreview",
                table: "ChatRooms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LastMessageSenderId",
                table: "ChatRooms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReceipts_RoomId",
                table: "MessageReceipts",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReceipts_UserId_Status_RoomId",
                table: "MessageReceipts",
                columns: new[] { "UserId", "Status", "RoomId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MessageReceipts_RoomId",
                table: "MessageReceipts");

            migrationBuilder.DropIndex(
                name: "IX_MessageReceipts_UserId_Status_RoomId",
                table: "MessageReceipts");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "MessageReceipts");

            migrationBuilder.DropColumn(
                name: "LastMessageAt",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "LastMessageId",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "LastMessagePreview",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "LastMessageSenderId",
                table: "ChatRooms");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReceipts_UserId_Status",
                table: "MessageReceipts",
                columns: new[] { "UserId", "Status" });
        }
    }
}
