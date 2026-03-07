using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
        public partial class AddChatRoomMemberDeleteAndClear : Migration
    {
                protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClearedAt",
                table: "ChatRoomMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ChatRoomMembers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ChatRoomMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

                protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClearedAt",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ChatRoomMembers");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ChatRoomMembers");
        }
    }
}
