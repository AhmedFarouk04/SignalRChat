using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.Enums;
using EnterpriseChat.Domain.ValueObjects;
using EnterpriseChat.Application.Interfaces;
using Microsoft.EntityFrameworkCore;


namespace EnterpriseChat.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder
{
    private readonly ChatDbContext _context;
    private readonly IPasswordHasher _hasher;

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

    public DatabaseSeeder(ChatDbContext context, IPasswordHasher hasher)
    {
        _context = context;
        _hasher = hasher;
    }

    public async Task SeedAsync()
    {
        await SeedUsersAsync();
        await SeedRoomsAsync();
    }

    private async Task SeedUsersAsync()
    {
        // ✅ كلمة سر ديمو قوية - تقدر تعمل بيها Login
        const string seedPassword = "P@ssw0rd!123";
        var seedHash = _hasher.Hash(seedPassword);

        var usersToEnsure = new List<ChatUser>
        {
            CreateSeedUser(_user1.Value,  "owner111", "owner111@gmail.com", "Owner 111", seedHash),
            CreateSeedUser(_user2.Value,  "user222",  "user222@gmail.com",  "User 222",  seedHash),
            CreateSeedUser(_user3.Value,  "user333",  "user333@gmail.com",  "User 333",  seedHash),
            CreateSeedUser(_user4.Value,  "user444",  "user444@gmail.com",  "User 444",  seedHash),
            CreateSeedUser(_user5.Value,  "user555",  "user555@gmail.com",  "User 555",  seedHash),
            CreateSeedUser(_user6.Value,  "user666",  "user666@gmail.com",  "User 666",  seedHash),
            CreateSeedUser(_user7.Value,  "user777",  "user777@gmail.com",  "User 777",  seedHash),
            CreateSeedUser(_user8.Value,  "user888",  "user888@gmail.com",  "User 888",  seedHash),
            CreateSeedUser(_user9.Value,  "user999",  "user999@gmail.com",  "User 999",  seedHash),
            CreateSeedUser(_user10.Value, "useraaa",  "useraaa@gmail.com",  "User AAA",  seedHash),
            CreateSeedUser(_user11.Value, "userbbb",  "userbbb@gmail.com",  "User BBB",  seedHash),
            CreateSeedUser(_user12.Value, "userccc",  "userccc@gmail.com",  "User CCC",  seedHash),
        };

        var existingIds = await _context.Users.AsNoTracking().Select(u => u.Id).ToListAsync();
        var missing = usersToEnsure.Where(u => !existingIds.Contains(u.Id)).ToList();
        if (missing.Count == 0) return;

        await _context.Users.AddRangeAsync(missing);
        await _context.SaveChangesAsync();
    }

    private static ChatUser CreateSeedUser(Guid id, string username, string email, string displayName, string passwordHash)
    {
        var user = new ChatUser(id, username, email, passwordHash);
        user.SetDisplayName(displayName);
        user.ConfirmEmail(); // ✅ confirmed users for dev
        return user;
    }

    private async Task SeedRoomsAsync()
    {
        if (await _context.ChatRooms.AnyAsync()) return;

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
