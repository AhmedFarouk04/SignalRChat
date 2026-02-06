using EnterpriseChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseChat.Infrastructure.Persistence;

public sealed class ChatDbContext : DbContext
{
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ChatRoom> ChatRooms => Set<ChatRoom>();
    public DbSet<BlockedUser> BlockedUsers => Set<BlockedUser>();
    public DbSet<MutedRoom> MutedRooms => Set<MutedRoom>();
    public DbSet<MessageReceipt> MessageReceipts => Set<MessageReceipt>();

    public DbSet<ChatUser> Users => Set<ChatUser>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Reaction> Reactions => Set<Reaction>();

    public ChatDbContext(DbContextOptions<ChatDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
