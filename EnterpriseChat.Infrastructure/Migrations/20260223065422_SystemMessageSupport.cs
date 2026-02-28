using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SystemMessageSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemMessage",
                table: "Messages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SystemMessageType",
                table: "Messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_RoomId_IsSystemMessage_CreatedAt",
                table: "Messages",
                columns: new[] { "RoomId", "IsSystemMessage", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_RoomId_IsSystemMessage_CreatedAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "IsSystemMessage",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "SystemMessageType",
                table: "Messages");
        }
    }
}
