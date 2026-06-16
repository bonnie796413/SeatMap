using BackEnd.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BackEnd.Data.Configurations;

public class AttendanceStateConfiguration : IEntityTypeConfiguration<AttendanceState>
{
    public void Configure(EntityTypeBuilder<AttendanceState> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.EmployeeId).IsUnique();
        builder.Property(a => a.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(a => a.LastCheckInAt).HasColumnType("timestamptz");
        builder.Property(a => a.LastCheckOutAt).HasColumnType("timestamptz");
    }
}
