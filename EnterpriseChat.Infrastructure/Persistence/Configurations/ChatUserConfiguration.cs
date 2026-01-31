using EnterpriseChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseChat.Infrastructure.Persistence.Configurations;

public sealed class ChatUserConfiguration : IEntityTypeConfiguration<ChatUser>
{
    public void Configure(EntityTypeBuilder<ChatUser> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Username)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.EmailConfirmed)
            .IsRequired();

        builder.Property(x => x.EmailOtpHash)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(x => x.EmailOtpExpiresAtUtc)
            .IsRequired(false);

        builder.Property(x => x.EmailOtpAttempts)
            .IsRequired();

        builder.Property(x => x.EmailOtpLastSentAtUtc)
            .IsRequired(false);

        // ✅ Uniques
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasIndex(x => x.Username).IsUnique();

        // optional: searching by displayname
        builder.HasIndex(x => x.DisplayName);
    }
}
