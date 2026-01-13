using EnterpriseChat.Domain.Entities;
using EnterpriseChat.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class BlockedUserConfiguration
    : IEntityTypeConfiguration<BlockedUser>
{
    public void Configure(EntityTypeBuilder<BlockedUser> builder)
    {
        builder.ToTable("BlockedUsers");

        builder.HasKey(x => new { x.BlockerId, x.BlockedId });

        builder.Property(x => x.BlockerId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(x => x.BlockedId)
            .HasConversion(id => id.Value, v => new UserId(v));

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => x.BlockerId);
        builder.HasIndex(x => x.BlockedId);
    }
}
