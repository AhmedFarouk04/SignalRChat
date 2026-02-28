using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FinalSetupPinnedMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2. تعديل الحقول (Preview)
            migrationBuilder.AlterColumn<string>(
                name: "LastReactionPreview",
                table: "ChatRooms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastMessagePreview",
                table: "ChatRooms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // 3. إنشاء جدول الـ PinnedMessages
            migrationBuilder.CreateTable(
                name: "PinnedMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PinnedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PinnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PinnedMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PinnedMessages_ChatRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ChatRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PinnedMessages_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id");
                });

            // 4. 🔥 الخطوة السحرية: إعادة إضافة الـ Foreign Key المفقود للرسائل
            migrationBuilder.AddForeignKey(
                name: "FK_Messages_ChatRooms_RoomId",
                table: "Messages",
                column: "RoomId",
                principalTable: "ChatRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // 5. إنشاء الـ Indexes
            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessages_MessageId",
                table: "PinnedMessages",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessages_PinnedAt",
                table: "PinnedMessages",
                column: "PinnedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PinnedMessages_RoomId_MessageId",
                table: "PinnedMessages",
                columns: new[] { "RoomId", "MessageId" },
                unique: true);
        }        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PinnedMessages");

            migrationBuilder.AlterColumn<string>(
                name: "LastReactionPreview",
                table: "ChatRooms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "LastMessagePreview",
                table: "ChatRooms",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_ChatRooms_RoomId",
                table: "Messages",
                column: "RoomId",
                principalTable: "ChatRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
