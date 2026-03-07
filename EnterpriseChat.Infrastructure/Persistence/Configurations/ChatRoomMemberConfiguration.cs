using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
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
            .HasColumnName("RoomId") 
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasConversion(v => v.Value, v => new UserId(v))
            .HasColumnName("UserId")
            .IsRequired();

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

        builder.Property(x => x.IsRemovedFromGroup)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.RemovedFromGroupAt)
            .IsRequired(false);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.LastReadMessageId);

    }
}