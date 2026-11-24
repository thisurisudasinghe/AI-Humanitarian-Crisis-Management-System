using Cms.Domain.Entities;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Infrastructure;

public class NamingConventionsTests
{
    [Theory]
    [InlineData("Users", "users")]
    [InlineData("OrganisationType", "organisation_type")]
    [InlineData("EmailHash", "email_hash")]
    [InlineData("RefreshTokens", "refresh_tokens")]
    [InlineData("UserID", "user_id")]
    [InlineData("IPAddress", "ip_address")]
    [InlineData("user_roles", "user_roles")]
    public void ToSnakeCase_maps_pascal_case_names(string input, string expected)
    {
        Assert.Equal(expected, NamingConventions.ToSnakeCase(input));
    }

    [Fact]
    public void ToSnakeCase_handles_empty_input()
    {
        Assert.Equal(string.Empty, NamingConventions.ToSnakeCase(string.Empty));
    }

    [Fact]
    public void ApplySnakeCase_preserves_only_legacy_notif_outbox_names()
    {
        using var context = new NamingConventionTestContext();

        var legacyOutbox = context.Model.FindEntityType(typeof(LegacyOutboxMessage))!;
        Assert.Equal("notif", legacyOutbox.GetSchema());
        Assert.Equal("OutboxMessages", legacyOutbox.GetTableName());
        Assert.Equal("PayloadJson", legacyOutbox.FindProperty(nameof(LegacyOutboxMessage.PayloadJson))!.GetColumnName());
        Assert.Equal("NextAttemptAt", legacyOutbox.FindProperty(nameof(LegacyOutboxMessage.NextAttemptAt))!.GetColumnName());
        Assert.Equal("LastError", legacyOutbox.FindProperty(nameof(LegacyOutboxMessage.LastError))!.GetColumnName());

        var legacyNotification = context.Model.FindEntityType(typeof(LegacyNotification))!;
        Assert.Equal("notif", legacyNotification.GetSchema());
        Assert.Equal("notifications", legacyNotification.GetTableName());
        Assert.Equal("read_at", legacyNotification.FindProperty(nameof(LegacyNotification.ReadAt))!.GetColumnName());

        var canonicalTestEntity = context.Model.FindEntityType(typeof(CanonicalOutboxMessage))!;
        Assert.Equal("common", canonicalTestEntity.GetSchema());
        Assert.Equal("outbox_messages", canonicalTestEntity.GetTableName());
        Assert.Equal("payload", canonicalTestEntity.FindProperty(nameof(CanonicalOutboxMessage.Payload))!.GetColumnName());

        var sql = context.Set<LegacyOutboxMessage>().ToQueryString();
        Assert.Contains("[notif].[OutboxMessages]", sql);
        Assert.Contains("[PayloadJson]", sql);
    }

    [Fact]
    public void Canonical_outbox_mapping_remains_common_snake_case()
    {
        var options = new DbContextOptionsBuilder<CmsDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CmsDb_OutboxFixNonexistent;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var context = new CmsDbContext(options);
        var outbox = context.Model.FindEntityType(typeof(OutboxMessage))!;

        Assert.Equal("common", outbox.GetSchema());
        Assert.Equal("outbox_messages", outbox.GetTableName());
        Assert.Equal("idempotency_key", outbox.FindProperty(nameof(OutboxMessage.IdempotencyKey))!.GetColumnName());
        Assert.Equal("next_retry_at", outbox.FindProperty(nameof(OutboxMessage.NextRetryAt))!.GetColumnName());
    }

    private sealed class LegacyOutboxMessage
    {
        public Guid Id { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime? NextAttemptAt { get; set; }
        public string? LastError { get; set; }
    }

    private sealed class LegacyNotification
    {
        public Guid Id { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    private sealed class CanonicalOutboxMessage
    {
        public Guid Id { get; set; }
        public string Payload { get; set; } = string.Empty;
    }

    private sealed class NamingConventionTestContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=CmsDb_OutboxFixNonexistent;Trusted_Connection=True;TrustServerCertificate=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LegacyOutboxMessage>(entity =>
            {
                entity.ToTable("OutboxMessages", "notif");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.PayloadJson).HasColumnName("PayloadJson");
                entity.Property(x => x.NextAttemptAt).HasColumnName("NextAttemptAt");
                entity.Property(x => x.LastError).HasColumnName("LastError");
            });

            modelBuilder.Entity<LegacyNotification>(entity =>
            {
                entity.ToTable("Notifications", "notif");
                entity.HasKey(x => x.Id);
            });

            modelBuilder.Entity<CanonicalOutboxMessage>(entity =>
            {
                entity.ToTable("OutboxMessages", "common");
                entity.HasKey(x => x.Id);
            });

            NamingConventions.ApplySnakeCase(modelBuilder);
        }
    }
}
