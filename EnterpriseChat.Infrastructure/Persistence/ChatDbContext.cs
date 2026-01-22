using EnterpriseChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

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
