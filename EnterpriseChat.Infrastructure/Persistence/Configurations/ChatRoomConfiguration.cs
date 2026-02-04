using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class ChatRoomConfiguration : IEntityTypeConfiguration<ChatRoom>
{
    public void Configure(EntityTypeBuilder<ChatRoom> builder)
    {
        builder.ToTable("ChatRooms");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(id => id.Value, value => new RoomId(value))
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(200);

        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.Property(x => x.OwnerId)
            .HasConversion(
                id => id!.Value,
                value => new UserId(value))
            .IsRequired(false);

        // ✅ اربط الـ navigation بالـ backing field
        builder.Metadata.FindNavigation(nameof(ChatRoom.Members))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(x => x.Members)          // ✅ مهم: HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(m => m.RoomId)        // ✅ FK الحقيقي
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<Message>()
            .WithOne()
            .HasForeignKey(m => m.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
