using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder
{
    private readonly ChatDbContext _context;

    private readonly UserId _user1 =
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"));

    private readonly UserId _user2 =
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    public DatabaseSeeder(ChatDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        await SeedRoomsAsync();
    }

    private async Task SeedRoomsAsync()
    {
        if (await _context.ChatRooms.AnyAsync())
            return;

        var generalRoom = new ChatRoom(
            name: "General",
            type: RoomType.Group,
            creatorId: _user1
        );

        generalRoom.AddMember(_user2);

        var techRoom = new ChatRoom(
            name: "Tech",
            type: RoomType.Group,
            creatorId: _user1
        );

        var rooms = new List<ChatRoom>
        {
            generalRoom,
            techRoom
        };

        await _context.ChatRooms.AddRangeAsync(rooms);
        await _context.SaveChangesAsync();
    }
}
