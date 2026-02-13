using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;  // ✅ هذا هو المفقود!
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class ChatRoomMemberConfiguration : IEntityTypeConfiguration<ChatRoomMember>
{
    public void Configure(EntityTypeBuilder<ChatRoomMember> builder)
    {
        builder.ToTable("ChatRoomMembers");

        builder.HasKey(x => new { x.RoomId, x.UserId });

        builder.Property(x => x.RoomId)
            .HasConversion(v => v.Value, v => new RoomId(v))
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasConversion(v => v.Value, v => new UserId(v))
            .HasColumnName("UserId")
            .IsRequired();

        // ✅ Shadow property nullable
        builder.Property<Guid?>("ChatUserId")
            .HasColumnName("ChatUserId")
            .IsRequired(false);

        builder.Property(x => x.LastReadMessageId)
            .HasConversion(
                v => v != null ? v.Value : (Guid?)null,
                v => v.HasValue ? new MessageId(v.Value) : null)
            .IsRequired(false);

        builder.Property(x => x.LastReadAt)
            .IsRequired(false);

        builder.Property(x => x.IsAdmin)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.IsOwner)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.JoinedAt)
            .IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.LastReadMessageId);
        builder.HasIndex(x => new { x.RoomId, x.UserId }).IsUnique();

        builder.HasOne<ChatRoom>()
            .WithMany(r => r.Members)
            .HasForeignKey(x => x.RoomId)
            .HasPrincipalKey(r => r.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // ✅ العلاقة مع ChatUser - Nullable FK مع SetNull
        builder.HasOne<ChatUser>()
            .WithMany()
            .HasForeignKey("ChatUserId")
            .HasPrincipalKey(u => u.Id)
            .OnDelete(DeleteBehavior.SetNull);
    }
}