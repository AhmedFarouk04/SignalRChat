using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder
{
    private readonly ChatDbContext _context;

    private readonly UserId _user1 = new(Guid.Parse("11111111-1111-1111-1111-111111111111")); // Owner
    private readonly UserId _user2 = new(Guid.Parse("22222222-2222-2222-2222-222222222222")); // Member

    private readonly UserId _user3 = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private readonly UserId _user4 = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private readonly UserId _user5 = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    private readonly UserId _user6 = new(Guid.Parse("66666666-6666-6666-6666-666666666666"));
    private readonly UserId _user7 = new(Guid.Parse("77777777-7777-7777-7777-777777777777"));
    private readonly UserId _user8 = new(Guid.Parse("88888888-8888-8888-8888-888888888888"));
    private readonly UserId _user9 = new(Guid.Parse("99999999-9999-9999-9999-999999999999"));
    private readonly UserId _user10 = new(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    private readonly UserId _user11 = new(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    private readonly UserId _user12 = new(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));

    public DatabaseSeeder(ChatDbContext context)
    {
        _context = context;
    }

    public async Task SeedAsync()
    {
        await SeedUsersAsync();
        await SeedRoomsAsync();
    }

    private async Task SeedUsersAsync()
    {
        var usersToEnsure = new List<ChatUser>
        {
            new ChatUser(_user1.Value,  "Owner 111",  "owner111@gmail.com"),
            new ChatUser(_user2.Value,  "User 222",   "user222@gmail.com"),
            new ChatUser(_user3.Value,  "User 333",   "user333@gmail.com"),
            new ChatUser(_user4.Value,  "User 444",   "user444@gmail.com"),
            new ChatUser(_user5.Value,  "User 555",   "user555@gmail.com"),
            new ChatUser(_user6.Value,  "User 666",   "user666@gmail.com"),
            new ChatUser(_user7.Value,  "User 777",   "user777@gmail.com"),
            new ChatUser(_user8.Value,  "User 888",   "user888@gmail.com"),
            new ChatUser(_user9.Value,  "User 999",   "user999@gmail.com"),
            new ChatUser(_user10.Value, "User AAA",   "useraaa@gmail.com"),
            new ChatUser(_user11.Value, "User BBB",   "userbbb@gmail.com"),
            new ChatUser(_user12.Value, "User CCC",   "userccc@gmail.com"),
        };

        var existingIds = await _context.Users
            .AsNoTracking()
            .Select(u => u.Id)
            .ToListAsync();

        var missing = usersToEnsure
            .Where(u => !existingIds.Contains(u.Id))
            .ToList();

        if (missing.Count == 0)
            return;

        await _context.Users.AddRangeAsync(missing);
        await _context.SaveChangesAsync();
    }

    private async Task SeedRoomsAsync()
    {
        if (await _context.ChatRooms.AnyAsync())
            return;

        var general = new ChatRoom("General", RoomType.Group, _user1);
        general.AddMember(_user2);
        general.AddMember(_user3);
        general.AddMember(_user4);
        general.AddMember(_user5);

        general.Members.First(m => m.UserId == _user2).PromoteToAdmin();
        general.Members.First(m => m.UserId == _user3).PromoteToAdmin();

        var tech = new ChatRoom("Tech", RoomType.Group, _user1);
        tech.AddMember(_user6);
        tech.AddMember(_user7);
        tech.AddMember(_user8);

        tech.Members.First(m => m.UserId == _user6).PromoteToAdmin();

        var management = new ChatRoom("Management", RoomType.Group, _user4);
        management.AddMember(_user1);
        management.AddMember(_user9);
        management.AddMember(_user10);

     
        var private_1_2 = ChatRoom.CreatePrivate(_user1, _user2);
        var private_1_3 = ChatRoom.CreatePrivate(_user1, _user3);
        var private_2_4 = ChatRoom.CreatePrivate(_user2, _user4);

        await _context.ChatRooms.AddRangeAsync(general, tech, management, private_1_2, private_1_3, private_2_4);
        await _context.SaveChangesAsync();
    }
}
