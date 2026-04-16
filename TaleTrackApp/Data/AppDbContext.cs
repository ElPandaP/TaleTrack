using Microsoft.EntityFrameworkCore;
using TaleTrackApp.Model;

namespace TaleTrackApp.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Media> Medias { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<TrackingEvent> TrackingEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cascade delete para limpiar datos relacionados
        modelBuilder.Entity<Review>()
            .HasOne(r => r.User)
            .WithMany(u => u.Reviews)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrackingEvent>()
            .HasOne(te => te.User)
            .WithMany(u => u.TrackingEvents)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
