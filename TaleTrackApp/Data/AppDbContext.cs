using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Model;

namespace TaleTrackApp.Data;

/// <summary>
/// Entity Framework Core context of the application: one table per entity in
/// <see cref="TaleTrackApp.Model"/>, plus the indexes, check constraints and delete rules
/// that the attributes on the entities cannot express.
/// </summary>
/// <param name="options">Provider and connection settings (PostgreSQL in production, SQLite in the tests).</param>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Registered accounts.</summary>
    public DbSet<User> Users { get; set; }

    /// <summary>Films, series and books, shared by every user that tracks them.</summary>
    public DbSet<Media> Medias { get; set; }

    /// <summary>Ratings and comments, at most one per user and media.</summary>
    public DbSet<Review> Reviews { get; set; }

    /// <summary>Each user's progress on a media, one row per user and media.</summary>
    public DbSet<TrackingEvent> TrackingEvents { get; set; }

    /// <summary>Friend requests and accepted friendships.</summary>
    public DbSet<Friendship> Friendships { get; set; }

    /// <summary>Login sessions, one per signed-in device.</summary>
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    /// <summary>Single-use tokens sent in email links (password reset, account deletion, signup revocation).</summary>
    public DbSet<AuthActionToken> AuthActionTokens { get; set; }

    /// <summary>Uploaded profile photos, one per user at most.</summary>
    public DbSet<UserAvatar> UserAvatars { get; set; }

    /// <summary>
    /// Configures what the entity attributes cannot: unique indexes, the per-type check
    /// constraints of <see cref="Media"/>, cascade deletes from <see cref="User"/> and the
    /// one-row-per-pair rule of <see cref="Friendship"/>.
    /// </summary>
    /// <param name="modelBuilder">Builder used to configure the model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // citext compares case-insensitively, so the unique indexes also reject "Lucas" next to
        // "lucas" (and "Ana@x.com" next to "ana@x.com"), matching the case-insensitive lookups in
        // UserService.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.Entity<User>()
            .Property(u => u.Username)
            .HasColumnType("citext");
        modelBuilder.Entity<User>()
            .Property(u => u.Email)
            .HasColumnType("citext");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Single table for every media type: the columns that only make sense for one type
        // are nullable, and the check constraints keep them empty for the others.
        modelBuilder.Entity<Media>(m =>
        {
            m.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);

            m.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Medias_Type", "\"Type\" IN ('Movie', 'Series', 'Book')");
                t.HasCheckConstraint("CK_Medias_Author_BookOnly", "\"Author\" IS NULL OR \"Type\" = 'Book'");
                t.HasCheckConstraint("CK_Medias_Isbn_BookOnly", "\"Isbn\" IS NULL OR \"Type\" = 'Book'");
                t.HasCheckConstraint("CK_Medias_SeasonEpisodeCounts_SeriesOnly",
                    "\"SeasonEpisodeCounts\" IS NULL OR \"Type\" = 'Series'");
            });

            // Lookups made by MediaService.FindOrCreateAsync on every tracking event.
            m.HasIndex(x => x.Isbn).HasFilter("\"Isbn\" IS NOT NULL");
            m.HasIndex(x => x.TitleEN);
            m.HasIndex(x => x.TitleES);
        });

        // Deleting a user deletes their reviews and tracking events.
        modelBuilder.Entity<Review>(r =>
        {
            r.HasOne(x => x.User)
                .WithMany(u => u.Reviews)
                .OnDelete(DeleteBehavior.Cascade);

            // One review per (user, media).
            r.HasIndex(x => new { x.UserId, x.MediaId }).IsUnique();
        });

        modelBuilder.Entity<TrackingEvent>(te =>
        {
            te.HasOne(x => x.User)
                .WithMany(u => u.TrackingEvents)
                .OnDelete(DeleteBehavior.Cascade);

            // One tracking event per (user, media); for a series it holds the latest
            // (season, episode) reported, not one row per episode.
            te.HasIndex(x => new { x.UserId, x.MediaId }).IsUnique();
        });

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

            f.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

            // One row per pair of users whichever of them sent the request, so A→B and B→A
            // can't coexist even when both are sent at the same time. The pair is normalized
            // into two stored generated columns because EF can't index an expression directly.
            // The functions are named differently on SQLite, which the tests run on.
            var (least, greatest) = Database.IsNpgsql() ? ("LEAST", "GREATEST") : ("min", "max");
            f.Property<Guid>("UserLowId")
                .HasComputedColumnSql($"{least}(\"RequesterId\", \"AddresseeId\")", stored: true);
            f.Property<Guid>("UserHighId")
                .HasComputedColumnSql($"{greatest}(\"RequesterId\", \"AddresseeId\")", stored: true);
            f.HasIndex("UserLowId", "UserHighId").IsUnique();
        });
    }
}
