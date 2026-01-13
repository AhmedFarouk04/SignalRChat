using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class ChatRoomMemberConfiguration
    : IEntityTypeConfiguration<ChatRoomMember>
{
    public void Configure(EntityTypeBuilder<ChatRoomMember> builder)
    {
        builder.ToTable("ChatRoomMembers");

        builder.HasKey(x => new
        {
            x.RoomId,
            x.UserId
        });

        builder.Property(x => x.RoomId)
            .HasConversion(
                id => id.Value,
                value => new RoomId(value))
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasConversion(
                id => id.Value,
                value => new UserId(value))
            .IsRequired();

        builder.HasIndex(x => x.UserId);
    }
}
