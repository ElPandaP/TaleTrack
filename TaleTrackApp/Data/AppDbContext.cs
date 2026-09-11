using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Model;

namespace TaleTrackApp.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Media> Medias { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<TrackingEvent> TrackingEvents { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<AuthActionToken> AuthActionTokens { get; set; }
    public DbSet<UserAvatar> UserAvatars { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cascade delete to clean up related data
        modelBuilder.Entity<Review>(r =>
        {
            r.HasOne(x => x.User)
                .WithMany(u => u.Reviews)
                .OnDelete(DeleteBehavior.Cascade);

            // One review per (user, media).
            r.HasIndex(x => new { x.UserId, x.MediaId }).IsUnique();
        });

        modelBuilder.Entity<TrackingEvent>()
            .HasOne(te => te.User)
            .WithMany(u => u.TrackingEvents)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RefreshToken>(rt =>
        {
            rt.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            rt.HasIndex(x => x.TokenHash).IsUnique();
        });

        modelBuilder.Entity<AuthActionToken>(at =>
        {
            at.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            at.HasIndex(x => x.TokenHash).IsUnique();
        });

        modelBuilder.Entity<UserAvatar>(av =>
        {
            av.HasKey(x => x.UserId);
            av.HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<UserAvatar>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Friendship>(f =>
        {
            f.HasOne(x => x.Requester)
                .WithMany()
                .HasForeignKey(x => x.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            f.HasOne(x => x.Addressee)
                .WithMany()
                .HasForeignKey(x => x.AddresseeId)
                .OnDelete(DeleteBehavior.Cascade);

            // One row per ordered pair.
            f.HasIndex(x => new { x.RequesterId, x.AddresseeId }).IsUnique();
        });
    }
}
