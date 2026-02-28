using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPinnedMessageConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessages_RoomId",
                table: "PinnedMessages",
                column: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PinnedMessages_RoomId",
                table: "PinnedMessages");
        }
    }
}
