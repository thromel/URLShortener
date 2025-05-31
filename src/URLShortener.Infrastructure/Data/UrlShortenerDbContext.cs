using Microsoft.EntityFrameworkCore;
using URLShortener.Core.Domain.Enhanced;
using URLShortener.Infrastructure.Data.Entities;

namespace URLShortener.Infrastructure.Data;

public class UrlShortenerDbContext : DbContext
{
    public UrlShortenerDbContext(DbContextOptions<UrlShortenerDbContext> options) : base(options)
    {
    }

    public DbSet<UrlEntity> Urls { get; set; }
    public DbSet<EventEntity> Events { get; set; }
    public DbSet<AnalyticsEntity> Analytics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure UrlEntity
        modelBuilder.Entity<UrlEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShortCode).IsUnique();
            entity.HasIndex(e => e.CreatedBy);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.ExpiresAt);
            entity.HasIndex(e => e.Status);

            entity.Property(e => e.ShortCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.OriginalUrl)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .IsRequired();

            entity.Property(e => e.Metadata)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>()
                );

            entity.Property(e => e.Tags)
                .HasColumnType("jsonb")
                .HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                );
        });

        // Configure EventEntity
        modelBuilder.Entity<EventEntity>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.HasIndex(e => e.AggregateId);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.OccurredAt);
            entity.HasIndex(e => new { e.AggregateId, e.Version }).IsUnique();

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.EventData)
                .IsRequired()
                .HasColumnType("jsonb");
        });

        // Configure AnalyticsEntity
        modelBuilder.Entity<AnalyticsEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShortCode);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Country);
            entity.HasIndex(e => e.DeviceType);

            entity.Property(e => e.ShortCode)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(e => e.IpAddress)
                .HasMaxLength(45); // IPv6 length

            entity.Property(e => e.UserAgent)
                .HasMaxLength(500);

            entity.Property(e => e.Referrer)
                .HasMaxLength(500);

            entity.Property(e => e.Country)
                .HasMaxLength(2);

            entity.Property(e => e.Region)
                .HasMaxLength(100);

            entity.Property(e => e.City)
                .HasMaxLength(100);

            entity.Property(e => e.DeviceType)
                .HasMaxLength(50);

            entity.Property(e => e.Browser)
                .HasMaxLength(50);

            entity.Property(e => e.OperatingSystem)
                .HasMaxLength(50);
        });
    }
}