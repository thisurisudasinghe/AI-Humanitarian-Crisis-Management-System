using Cms.Domain.Entities.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cms.Infrastructure.Data.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("Requests");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.HouseholdId)
            .IsRequired();

        builder.Property(r => r.CrisisId)
            .IsRequired();

        builder.Property(r => r.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(r => r.Category)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(r => r.Urgency)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(r => r.SubmittedAt)
            .IsRequired();

        builder.Property(r => r.AssignedCoordinatorId)
            .HasMaxLength(450);

        builder.Property(r => r.Remarks)
            .HasMaxLength(1000);

        builder.Property(r => r.IsDeleted)
            .IsRequired();

        builder.HasOne(r => r.Household)
            .WithMany()
            .HasForeignKey(r => r.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Crisis)
            .WithMany()
            .HasForeignKey(r => r.CrisisId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.HouseholdId);
        builder.HasIndex(r => r.CrisisId);
        builder.HasIndex(r => r.Status);
    }
}
