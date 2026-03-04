using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseChat.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageDeletions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.CreateTable(
                name: "MessageDeletions",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageDeletions", x => new { x.MessageId, x.UserId });
                    table.ForeignKey(
                        name: "FK_MessageDeletions_Messages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "Messages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeletions_UserId",
                table: "MessageDeletions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
    name: "FK_Messages_Messages_ReplyToMessageId",
    table: "Messages",
    column: "ReplyToMessageId",
    principalTable: "Messages",
    principalColumn: "Id",
    onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Messages_ReplyToMessageId",
                table: "Messages");

            migrationBuilder.DropTable(
                name: "MessageDeletions");

            migrationBuilder.AddForeignKey(
    name: "FK_Messages_Messages_ReplyToMessageId",
    table: "Messages",
    column: "ReplyToMessageId",
    principalTable: "Messages",
    principalColumn: "Id",
    onDelete: ReferentialAction.NoAction);
        }
    }
}
