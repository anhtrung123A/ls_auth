using app.Entities;
using Microsoft.EntityFrameworkCore;

namespace app.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasIndex(x => x.LoginId).IsUnique();
            entity.Property(x => x.LoginId).HasColumnName("login_id");
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
            entity.Property(x => x.RegisteredAtUtc).HasColumnName("registered_at_utc");
            entity.Property(x => x.PasswordChangedAtUtc).HasColumnName("password_changed_at_utc");
            entity.Property(x => x.RegisteredByEmail).HasColumnName("registered_by_email");
            entity.Property(x => x.UpdatedByEmail).HasColumnName("updated_by_email");
        });

        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.ToTable("user_sessions");
            entity.HasIndex(x => x.RefreshTokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc");
            entity.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
            entity.Property(x => x.ReplacedBySessionId).HasColumnName("replaced_by_session_id");

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
