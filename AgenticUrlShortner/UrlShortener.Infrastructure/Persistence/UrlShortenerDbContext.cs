using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain;

namespace UrlShortener.Infrastructure.Persistence;

public class UrlShortenerDbContext : DbContext
{
    public UrlShortenerDbContext(
        DbContextOptions<UrlShortenerDbContext> options)
        : base(options)
    {
    }

    public DbSet<UrlMapping> UrlMappings => Set<UrlMapping>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UrlMapping>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.ShortCode)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(x => x.OriginalUrl)
                .IsRequired()
                .HasMaxLength(2048);

            entity.HasIndex(x => x.ShortCode)
                .IsUnique();

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.Property(x => x.ClickCount)
                .IsRequired();

            entity.Property(x => x.IsActive)
                .IsRequired();
        });
    }
}