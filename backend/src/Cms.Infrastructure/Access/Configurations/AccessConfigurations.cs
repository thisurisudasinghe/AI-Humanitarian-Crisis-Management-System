using Cms.Domain.Entities.Access;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Access.Configurations;

public sealed class StaffAccessApplicationConfiguration
    : IEntityTypeConfiguration<StaffAccessApplication>
{
    public void Configure(EntityTypeBuilder<StaffAccessApplication> builder)
    {
        builder.ToTable("staff_access_applications", "access");
        builder.HasKey(a => a.Id).HasName("PK_staff_access_applications");

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.ApplicantUserId).HasColumnName("applicant_user_id").IsRequired();
        builder.Property(a => a.Reference).HasColumnName("reference").IsRequired().HasMaxLength(32);
        builder.Property(a => a.FullName).HasColumnName("full_name").IsRequired().HasMaxLength(1000);
        builder.Property(a => a.Email).HasColumnName("email").IsRequired().HasMaxLength(1000);
        builder.Property(a => a.Organization).HasColumnName("organization").IsRequired().HasMaxLength(1000);
        builder.Property(a => a.Designation).HasColumnName("designation").IsRequired().HasMaxLength(1000);
        builder.Property(a => a.RequestedRole).HasColumnName("requested_role").IsRequired().HasMaxLength(100);
        builder.Property(a => a.Reason).HasColumnName("reason").IsRequired().HasMaxLength(4000);
        builder.Property(a => a.Status).HasColumnName("status").IsRequired().HasMaxLength(40)
            .HasConversion<string>();
        builder.Property(a => a.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.Property(a => a.ReviewedAt).HasColumnName("reviewed_at");
        builder.Property(a => a.ReviewerUserId).HasColumnName("reviewer_user_id");
        builder.Property(a => a.RejectionReason).HasColumnName("rejection_reason").HasMaxLength(4000);
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(a => a.Reference).IsUnique()
            .HasDatabaseName("UX_staff_access_applications_reference");
        builder.HasIndex(a => new { a.ApplicantUserId, a.Status })
            .HasDatabaseName("IX_staff_access_applications_applicant_status");
        builder.HasIndex(a => a.Status).HasDatabaseName("IX_staff_access_applications_status");
    }
}

public sealed class GuestReportConfiguration : IEntityTypeConfiguration<GuestReport>
{
    public void Configure(EntityTypeBuilder<GuestReport> builder)
    {
        builder.ToTable("guest_reports", "access");
        builder.HasKey(r => r.Id).HasName("PK_guest_reports");

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.Reference).HasColumnName("reference").IsRequired().HasMaxLength(32);
        builder.Property(r => r.TokenHash).HasColumnName("token_hash").IsRequired().HasMaxLength(128);
        builder.Property(r => r.IncidentType).HasColumnName("incident_type").IsRequired().HasMaxLength(100);
        builder.Property(r => r.Location).HasColumnName("location").IsRequired().HasMaxLength(500);
        builder.Property(r => r.Latitude).HasColumnName("latitude").HasPrecision(18, 7);
        builder.Property(r => r.Longitude).HasColumnName("longitude").HasPrecision(18, 7);
        builder.Property(r => r.Description).HasColumnName("description").IsRequired().HasMaxLength(4000);
        builder.Property(r => r.ContactInfo).HasColumnName("contact_info").HasMaxLength(2000);
        builder.Property(r => r.Status).HasColumnName("status").IsRequired().HasMaxLength(40)
            .HasConversion<string>();
        builder.Property(r => r.SubmittedAt).HasColumnName("submitted_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(r => r.Reference).IsUnique().HasDatabaseName("UX_guest_reports_reference");
        builder.HasIndex(r => r.TokenHash).IsUnique().HasDatabaseName("UX_guest_reports_token_hash");
        builder.HasIndex(r => r.Status).HasDatabaseName("IX_guest_reports_status");

        builder.HasMany(r => r.Timeline)
            .WithOne()
            .HasForeignKey(entry => entry.ReportId)
            .HasConstraintName("FK_guest_report_timeline_reports")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GuestReportTimelineConfiguration : IEntityTypeConfiguration<GuestReportTimeline>
{
    public void Configure(EntityTypeBuilder<GuestReportTimeline> builder)
    {
        builder.ToTable("guest_report_timeline", "access");
        builder.HasKey(entry => entry.Id).HasName("PK_guest_report_timeline");

        builder.Property(entry => entry.Id).HasColumnName("id");
        builder.Property(entry => entry.ReportId).HasColumnName("report_id").IsRequired();
        builder.Property(entry => entry.Status).HasColumnName("status").IsRequired().HasMaxLength(40)
            .HasConversion<string>();
        builder.Property(entry => entry.Note).HasColumnName("note").HasMaxLength(4000);
        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(entry => new { entry.ReportId, entry.CreatedAt })
            .HasDatabaseName("IX_guest_report_timeline_report_created");
    }
}
