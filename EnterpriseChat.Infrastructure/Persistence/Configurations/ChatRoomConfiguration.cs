using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class ChatRoomConfiguration
    : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(
                id => id.Value,
                value => new RoomId(value))
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200);

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.OwnerId)
            .HasConversion(
                id => id!.Value,
                value => new UserId(value))
            .IsRequired(false);

        builder.HasMany<ChatRoomMember>()
            .WithOne()
            .HasForeignKey(x => x.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
      


    }
}
