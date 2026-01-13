using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class MutedRoomConfiguration
    : IEntityTypeConfiguration<MutedRoom>
{
    public void Configure(EntityTypeBuilder<MutedRoom> builder)
    {
        builder.ToTable("MutedRooms");

        builder.HasKey(x => new { x.RoomId, x.UserId });

        builder.Property(x => x.RoomId)
            .HasConversion(id => id.Value, v => new RoomId(v));

        builder.Property(x => x.UserId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(x => x.MutedAt)
            .IsRequired();

        builder.HasIndex(x => x.UserId);
    }
}
